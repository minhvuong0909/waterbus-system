using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ValidationException = WaterbusSystem.Application.Common.Exceptions.ValidationException;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Domain.Common;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;

namespace WaterbusSystem.Application.Features.Bookings.Commands.CreateBooking;

/// <summary>
/// Một hành khách sắp ngồi trên một ghế. BR-COM-06: tên bắt buộc, email/điện thoại tùy chọn.
/// BR-COM-05: Purchaser có thể mua vé cho người khác, không nhất thiết là người lên tàu.
/// </summary>
public record PassengerInput(string Name, string? Email, string? Phone);

/// <summary>
/// DTO trả về sau khi tạo Order + Booking và giữ chỗ thành công.
/// BookingId/TotalAmount/Status giữ nguyên tên cũ để các client hiện có không phải đổi.
/// </summary>
public record BookingResponseDto(
    Guid BookingId,
    string BookingCode,
    decimal TotalAmount,
    string Status,
    int HoldDurationSeconds,
    Guid OrderId = default,
    string OrderStatus = "",
    Guid BoardingCallId = default,
    Guid DisembarkingCallId = default,
    int BoardingStopOrder = 0,
    int DisembarkingStopOrder = 0,
    string? CheckoutSessionId = null);

/// <summary>
/// BR-HOLD-01/02: CheckoutSession được định danh theo Order chứ không theo UserId, nhờ vậy
/// Guest không cần đăng nhập vẫn dùng được, và cùng một phiên phục vụ được cho cả hai Booking
/// của một Order round-trip.
/// </summary>
public static class CheckoutSessionKey
{
    public static string ForOrder(Guid orderId) => $"order:{orderId}";
}

/// <summary>
/// BR-COM-08: Checkout Review tạo Order/Booking/SeatReservation ở trạng thái nháp,
/// CHƯA phát hành Ticket. Ticket chỉ được phát hành sau khi backend xác minh thanh toán thành công.
/// BR-ROUTE-02/06/07: Booking chỉ chọn cặp boarding - disembarking là hai TripStopCall thực sự
/// được phục vụ trên đúng Trip, đúng chiều di chuyển. Không suy từ Station.OrderIndex.
/// BR-SEAT-04/05: Ghế được giữ theo chặng [BoardingStopOrder, DisembarkingStopOrder) tính bằng
/// VisitOrder của TripStopCall, nên cùng một ghế vẫn bán lại được cho chặng không chồng lấn.
/// BR-FARE-01/02: Giá chỉ theo TripType x SeatClass, phẳng - không theo đoạn đi hay bến lên/xuống.
/// </summary>
public record CreateBookingCommand(
    Guid TripId,
    List<Guid> SeatIds,
    Guid BoardingStationId,
    Guid DisembarkingStationId,
    string CustomerName,
    string CustomerEmail,
    string CustomerPhone,
    IReadOnlyList<PassengerInput>? Passengers = null,
    Guid? BoardingCallId = null,
    Guid? DisembarkingCallId = null) : IRequest<BookingResponseDto>;

/// <summary>
/// Validator kiểm tra tính toàn vẹn của dữ liệu đầu vào
/// </summary>
public class CreateBookingCommandValidator : AbstractValidator<CreateBookingCommand>
{
    public CreateBookingCommandValidator()
    {
        RuleFor(x => x.TripId)
            .NotEmpty().WithMessage("Mã chuyến tàu không được để trống.");

        RuleFor(x => x.BoardingStationId)
            .NotEmpty().WithMessage("Bạn phải chọn bến lên tàu.");

        RuleFor(x => x.DisembarkingStationId)
            .NotEmpty().WithMessage("Bạn phải chọn bến xuống tàu.")
            .NotEqual(x => x.BoardingStationId)
                .WithMessage("Bến lên và bến xuống không được trùng nhau nếu chuyến chỉ phục vụ mỗi bến một lần.");

        RuleFor(x => x.SeatIds)
            .NotEmpty().WithMessage("Bạn phải chọn ít nhất một ghế.")
            .Must(seats => seats.Count <= 10).WithMessage("Mỗi lượt đặt tối đa 10 ghế.")
            .Must(seats => seats.Distinct().Count() == seats.Count)
                .WithMessage("Danh sách ghế không được chứa ghế trùng lặp.");

        // BR-COM-06: nếu khách khai báo danh sách hành khách thì phải khớp 1-1 với ghế đã chọn.
        RuleFor(x => x.Passengers)
            .Must((cmd, p) => p == null || p.Count == 0 || p.Count == cmd.SeatIds.Count)
                .WithMessage("Danh sách hành khách phải khớp với số ghế đã chọn.");

        RuleForEach(x => x.Passengers).ChildRules(passenger =>
        {
            passenger.RuleFor(p => p.Name).NotEmpty().WithMessage("Tên hành khách không được để trống.");
        });

        RuleFor(x => x.CustomerName)
            .NotEmpty().WithMessage("Họ và tên người mua không được để trống.")
            .MaximumLength(150).WithMessage("Họ tên không được vượt quá 150 ký tự.");

        RuleFor(x => x.CustomerEmail)
            .NotEmpty().WithMessage("Email người mua không được để trống.")
            .EmailAddress().WithMessage("Định dạng email không hợp lệ.");

        RuleFor(x => x.CustomerPhone)
            .NotEmpty().WithMessage("Số điện thoại không được để trống.")
            .Matches(@"^[0-9]{10,11}$").WithMessage("Số điện thoại phải từ 10 đến 11 chữ số.");
    }
}

/// <summary>
/// Handler xử lý nghiệp vụ đặt vé với 2 LỚP PHÒNG THỦ CHỐNG DOUBLE-BOOKING / OVERSELLING:
/// - Lớp 1 (RAM/Cache): Redis lock SET NX PX khóa tài nguyên ghế trong đúng critical section
///   "kiểm tra chặng rồi ghi DB", KHÔNG phải cơ chế giữ chỗ. TTL ngắn (30s) để một crash không
///   khóa cứng ghế cho mọi chặng khác trong 10 phút, triệt tiêu tác dụng bán lại ghế theo chặng.
/// - Lớp 2 (Database): Segment Overlap trên SeatReservation + RowVersion concurrency token.
///
/// Luồng nghiệp vụ BR v2.0: PurchaseOrder -> Booking -> SeatReservation. Ticket KHÔNG được tạo ở đây.
/// </summary>
public class CreateBookingCommandHandler : IRequestHandler<CreateBookingCommand, BookingResponseDto>
{
    /// <summary>BR-HOLD-01/C02: cửa sổ giữ chỗ. Được cấu hình, không hard-code nghiệp vụ bất biến.</summary>
    private static readonly TimeSpan HoldDuration = TimeSpan.FromMinutes(10);

    /// <summary>BR-TIME-01: chỉ mở bán khi chuyến chưa rời bến đầu. EnRoute trở đi là đã đóng bán.</summary>
    private static readonly TripStatus[] SellableTripStatuses = [TripStatus.Scheduled, TripStatus.Boarding];

    private readonly IApplicationDbContext _context;
    private readonly IDistributedLockService _lockService;
    private readonly ISeatAvailabilityService _seatAvailabilityService;
    private readonly ICheckoutSessionService? _checkoutSessionService;

    public CreateBookingCommandHandler(
        IApplicationDbContext context,
        IDistributedLockService lockService,
        ISeatAvailabilityService seatAvailabilityService,
        ICheckoutSessionService? checkoutSessionService = null)
    {
        _context = context;
        _lockService = lockService;
        _seatAvailabilityService = seatAvailabilityService;
        _checkoutSessionService = checkoutSessionService;
    }

    public async Task<BookingResponseDto> Handle(CreateBookingCommand request, CancellationToken cancellationToken)
    {
        // =========================================================================
        // LỚP PHÒNG THỦ 1: REDIS LOCK (mutex ngắn hạn cho critical section)
        // =========================================================================
        var acquiredLocks = new List<IAsyncDisposable>();

        try
        {
            var requestedSeatIds = request.SeatIds.Distinct().ToList();

            foreach (var seatId in requestedSeatIds)
            {
                var lockKey = $"lock:trip:{request.TripId}:seat:{seatId}";

                var lockHandle = await _lockService.AcquireLockAsync(
                    lockKey,
                    expiryTime: TimeSpan.FromSeconds(30),
                    waitTime: TimeSpan.FromSeconds(2),
                    retryTime: TimeSpan.FromMilliseconds(100),
                    cancellationToken);

                if (lockHandle == null)
                {
                    throw new SeatAlreadyBookedException(seatId.ToString());
                }

                acquiredLocks.Add(lockHandle);
            }

            var now = DateTimeOffset.UtcNow;

            // =========================================================================
            // Nạp Trip kèm Route và các TripStopCall - nguồn sự thật duy nhất cho
            // "chuyến này thực sự phục vụ bến nào, theo thứ tự nào" (BR-ROUTE-02/06/07).
            // Load tracked (không AsNoTracking) vì SetJourney gán navigation TripStopCall
            // vào Booking mới; nếu untracked, EF có thể nhận nhầm là bản ghi mới cần INSERT.
            // =========================================================================
            var trip = await _context.Trips
                .Include(t => t.Route)
                .Include(t => t.TripStopCalls)
                    .ThenInclude(c => c.RouteStop)
                .FirstOrDefaultAsync(t => t.Id == request.TripId, cancellationToken)
                ?? throw new NotFoundException(nameof(Trip), request.TripId);

            EnsureTripIsSellable(trip, now);

            // BR-ROUTE-03: TripType của Trip phải khớp loại dịch vụ của Route.
            if (trip.Route == null)
            {
                throw new NotFoundException(nameof(Route), trip.RouteId);
            }

            if (!string.Equals(trip.Route.ServiceType, ToServiceType(trip.TripType), StringComparison.OrdinalIgnoreCase))
            {
                throw Validation("Loại dịch vụ của chuyến không khớp với tuyến, không thể đặt vé.");
            }

            // =========================================================================
            // Xác định cặp TripStopCall thực sự được phục vụ (BR-ROUTE-02/06/07)
            // =========================================================================
            var (boardingCall, disembarkingCall) = ResolveJourneyCalls(
                trip, request.BoardingStationId, request.DisembarkingStationId,
                request.BoardingCallId, request.DisembarkingCallId);

            var boardingStopOrder = boardingCall.VisitOrder;
            var disembarkingStopOrder = disembarkingCall.VisitOrder;

            // =========================================================================
            // Kiểm tra ghế thuộc đúng tàu vận hành chuyến (BR-SEAT-06)
            // =========================================================================
            var seats = await _context.Seats
                .Include(s => s.SeatClass)
                .Where(s => requestedSeatIds.Contains(s.Id) && s.IsActive)
                .ToListAsync(cancellationToken);

            if (seats.Count != requestedSeatIds.Count)
            {
                throw new NotFoundException(nameof(Seat), string.Join(",", requestedSeatIds));
            }

            var wrongBoatSeats = seats.Where(s => s.BoatId != trip.BoatId).ToList();
            if (wrongBoatSeats.Count != 0)
            {
                throw Validation(
                    $"Ghế '{string.Join(", ", wrongBoatSeats.Select(s => s.SeatCode))}' không thuộc tàu vận hành chuyến này.");
            }

            var seatsWithoutClass = seats.Where(s => s.SeatClass == null).ToList();
            if (seatsWithoutClass.Count != 0)
            {
                throw Validation(
                    $"Ghế '{string.Join(", ", seatsWithoutClass.Select(s => s.SeatCode))}' chưa được gán hạng ghế nên chưa thể tính giá.");
            }

            // =========================================================================
            // BR-FARE-01/02: giá phẳng theo TripType x SeatClass, hiệu lực tại thời điểm mua
            // =========================================================================
            var fares = await LoadFaresAsync(trip.TripType, now, cancellationToken);
            var fareBySeatClassId = fares.ToDictionary(f => f.SeatClassId, f => f);

            var missingFare = seats.FirstOrDefault(s => !fareBySeatClassId.ContainsKey(s.SeatClassId));
            if (missingFare != null)
            {
                throw Validation(
                    $"Chưa cấu hình giá vé cho hạng ghế '{missingFare.SeatClass!.Name}' trên chuyến {trip.TripType}. " +
                    "Vui lòng liên hệ quản trị viên.");
            }

            // =========================================================================
            // LỚP PHÒNG THỦ 2: KIỂM TRA ĐỤNG ĐỘ CHẶNG (SEGMENT OVERLAP) TẠI CSDL
            // =========================================================================
            var availableSeatIds = await _seatAvailabilityService.GetAvailableSeatsAsync(
                trip.Id, requestedSeatIds, boardingStopOrder, disembarkingStopOrder, cancellationToken);

            var unavailableSeatIds = requestedSeatIds.Except(availableSeatIds).ToList();
            if (unavailableSeatIds.Count != 0)
            {
                throw new ConcurrencyException("Một hoặc nhiều ghế bạn chọn đã được đặt trên đoạn chặng này!");
            }

            // =========================================================================
            // Tạo PurchaseOrder -> Booking -> SeatReservations (chưa phát hành Ticket)
            // =========================================================================
            var passengers = ResolvePassengers(request);
            var order = new PurchaseOrder
            {
                // BR-ID-01: Guest mua vé không cần account -> AccountId để null.
                PurchaserName = request.CustomerName,
                PurchaserEmail = request.CustomerEmail,
                PurchaserPhone = request.CustomerPhone,
                PurchaseMode = PurchaseMode.OneWay,
                Status = OrderStatus.PendingPayment,
                Currency = "VND",
                ExpiresAt = now.Add(HoldDuration)
            };

            var booking = new Booking
            {
                Order = order,
                OrderId = order.Id,
                BookingCode = CodeGenerator.GenerateBookingCode(now),
                CustomerName = request.CustomerName,
                CustomerEmail = request.CustomerEmail,
                CustomerPhone = request.CustomerPhone,
                Status = BookingStatus.PendingPayment,
                PaymentStatus = PaymentStatus.Pending
            };

            // SetJourney tự validate: cùng Trip, VisitOrder tăng, cho phép lên/xuống,
            // và chấp nhận 2 lần ghé cùng Station ở 2 VisitOrder khác (chuyến vòng sightseeing).
            booking.SetJourney(boardingCall, disembarkingCall);

            // BR-COM-05/06: danh sách hành khách được khách gửi theo đúng thứ tự SeatIds, nên phải ghép
            // hành khách với ghế theo THỨ TỰ CLIENT yêu cầu. CSDL không bảo đảm thứ tự trả về,
            // dùng trực tiếp kết quả query sẽ khiến tên khách bị gắn nhầm sang ghế khác.
            var seatById = seats.ToDictionary(s => s.Id);
            var orderedSeats = requestedSeatIds.Select(seatId => seatById[seatId]).ToList();

            var quotedTotal = 0m;
            for (var i = 0; i < orderedSeats.Count; i++)
            {
                var seat = orderedSeats[i];
                var fare = fareBySeatClassId[seat.SeatClassId].Price;
                var passenger = passengers[i];

                // BR-SEAT-03: giữ snapshot hành khách + ghế + giá, để xuất Ticket về sau.
                booking.SeatReservations.Add(new SeatReservation
                {
                    Booking = booking,
                    BookingId = booking.Id,
                    TripId = trip.Id,
                    SeatId = seat.Id,
                    BoardingStopOrder = boardingStopOrder,
                    DisembarkingStopOrder = disembarkingStopOrder,
                    Status = ReservationStatus.Pending,
                    PassengerName = passenger.Name,
                    PassengerEmail = passenger.Email,
                    PassengerPhone = passenger.Phone,
                    QuotedFare = fare,
                    SeatCodeAtSale = seat.SeatCode,
                    SeatClassAtSale = seat.SeatClass!.Name
                });

                quotedTotal += fare;
            }

            order.QuotedTotal = quotedTotal;
            booking.QuotedTotal = quotedTotal;

            // BR-COM-01: Order sở hữu Booking. Phải đưa Booking vào collection của Order
            // để ValidateForCheckout() nhìn thấy đúng số lượng Booking thuộc Order này.
            order.Bookings.Add(booking);

            // BR-COM-08: kiểm tra số liều lệ khi submit checkout.
            order.ValidateForCheckout();

            _context.PurchaseOrders.Add(order);
            _context.Bookings.Add(booking);

            await _context.SaveChangesAsync(cancellationToken);

            // BR-HOLD-01/02: đăng ký phiên giữ ghế để FE đếm ngược và để bước thanh toán mở
            // cửa sổ PaymentPending. Hold thật vẫn nằm ở SeatReservation trong SQL Server
            // (BR-HOLD-04) - Redis chỉ hỗ trợ trải nghiệm, không thay thế tính nhất quán CSDL.
            string? sessionId = null;
            int remainingSeconds = (int)HoldDuration.TotalSeconds;
            if (_checkoutSessionService is not null)
            {
                sessionId = CheckoutSessionKey.ForOrder(order.Id);
                try
                {
                    var session = await _checkoutSessionService.HoldAsync(
                        sessionId,
                        order.Id,
                        orderedSeats.Select(s => new SeatHold(trip.Id, s.Id, boardingStopOrder, disembarkingStopOrder)).ToList(),
                        cancellationToken);

                    remainingSeconds = Math.Max(0, (int)(session.ExpiresAt - now).TotalSeconds);
                }
                catch (Exception)
                {
                    // BR-HOLD-04: Redis chỉ hỗ trợ trải nghiệm. Order/Booking/SeatReservation đã
                    // ghi xong ở CSDL nên tuyệt đối KHÔNG ném lỗi ra ngoài - nếu ném, khách thấy
                    // lỗi rồi bấm lại sẽ tạo thêm một Order trùng. Hold thật vẫn còn nguyên vì
                    // nó nằm ở SeatReservation, và ExpiredBookingCleanupService sẽ dọn theo TTL.
                    remainingSeconds = (int)HoldDuration.TotalSeconds;
                }
            }

            return new BookingResponseDto(
                booking.Id,
                booking.BookingCode,
                booking.QuotedTotal,
                booking.Status.ToString(),
                HoldDurationSeconds: remainingSeconds,
                OrderId: order.Id,
                OrderStatus: order.Status.ToString(),
                BoardingCallId: boardingCall.Id,
                DisembarkingCallId: disembarkingCall.Id,
                BoardingStopOrder: boardingStopOrder,
                DisembarkingStopOrder: disembarkingStopOrder,
                CheckoutSessionId: sessionId);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyException("Xung đột dữ liệu khi cập nhật ghế. Vui lòng thử lại!", ex);
        }
        finally
        {
            // Luôn giải phóng lock ngay khi kết thúc critical section, dù thành công hay thất bại.
            foreach (var acquiredLock in acquiredLocks)
            {
                await acquiredLock.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// BR-SRCH-02 / BR-TIME-01 / BR-SCH-04: Trip chỉ bán được khi chưa hủy, đã có Boat
    /// (inventory + loại ghế lấy từ Boat) và chưa vượt mốc đóng bán do Admin cấu hình.
    /// </summary>
    private static void EnsureTripIsSellable(Trip trip, DateTimeOffset now)
    {
        if (!SellableTripStatuses.Contains(trip.Status))
        {
            throw Validation($"Chuyến tàu hiện đang ở trạng thái {trip.Status} nên không nhận đặt vé mới.");
        }

        if (trip.BoatId == null)
        {
            throw Validation("Chuyến tàu chưa được phân công tàu nên chưa mở bán.");
        }

        if (trip.SalesCloseAt.HasValue && trip.SalesCloseAt.Value <= now)
        {
            throw Validation("Chuyến tàu đã đóng bán vé.");
        }
    }

    /// <summary>
    /// BR-ROUTE-06: một chuyến vòng Sightseeing có thể lên và xuống tại cùng một Station vật lý,
    /// nhưng phải là hai lần ghé khác nhau theo VisitOrder. Vì vậy khi client chỉ gửi StationId,
    /// mặc định lấy lần ghé ĐẦU để lên và lần ghé CUỐI để xuống. Client có thể gửi thẳng
    /// BoardingCallId/DisembarkingCallId khi cần chọn lần ghé cụ thể.
    /// </summary>
    private static (TripStopCall Boarding, TripStopCall Disembarking) ResolveJourneyCalls(
        Trip trip, Guid boardingStationId, Guid disembarkingStationId,
        Guid? requestedBoardingCallId, Guid? requestedDisembarkingCallId)
    {
        var calls = trip.TripStopCalls
            .Where(c => c.RouteStop != null)
            .OrderBy(c => c.VisitOrder)
            .ToList();

        if (calls.Count < 2)
        {
            throw Validation("Chuyến tàu chưa khai báo đủ hai điểm dừng nên chưa thể mở bán.");
        }

        TripStopCall Resolve(Guid stationId, Guid? requestedCallId, bool isBoarding, string fieldName)
        {
            if (requestedCallId.HasValue)
            {
                var explicitCall = calls.FirstOrDefault(c => c.Id == requestedCallId.Value);
                if (explicitCall == null)
                {
                    throw Validation("Điểm dừng được chỉ định không thuộc chuyến tàu này.");
                }

                if (explicitCall.RouteStop!.StationId != stationId)
                {
                    throw Validation(
                        $"{(isBoarding ? "Bến lên" : "Bến xuống")} không khớp với điểm dừng được chỉ định.");
                }

                return explicitCall;
            }

            var stationCalls = calls.Where(c => c.RouteStop!.StationId == stationId).ToList();
            if (stationCalls.Count == 0)
            {
                throw Validation(
                    $"{(isBoarding ? "Bến lên" : "Bến xuống")} không nằm trên hành trình được phục vụ của chuyến tàu này.");
            }

            return isBoarding ? stationCalls[0] : stationCalls[^1];
        }

        var boardingCall = Resolve(boardingStationId, requestedBoardingCallId, isBoarding: true, nameof(CreateBookingCommand.BoardingStationId));
        var disembarkingCall = Resolve(disembarkingStationId, requestedDisembarkingCallId, isBoarding: false, nameof(CreateBookingCommand.DisembarkingStationId));

        try
        {
            ValidateVisitOrder(boardingCall, disembarkingCall);
        }
        catch (ArgumentException ex)
        {
            throw Validation(ex.Message);
        }

        return (boardingCall, disembarkingCall);
    }

    private static void ValidateVisitOrder(TripStopCall boarding, TripStopCall disembarking)
    {
        if (boarding.Id == disembarking.Id || boarding.VisitOrder >= disembarking.VisitOrder)
        {
            throw new ArgumentException("Bến xuống phải nằm sau bến lên theo thứ tự hành trình của chuyến tàu.");
        }

        if (boarding.RouteStop!.CanBoard == false)
        {
            throw new ArgumentException("Bến lên được chọn không cho phép khách lên tàu.");
        }

        if (disembarking.RouteStop!.CanAlight == false)
        {
            throw new ArgumentException("Bến xuống được chọn không cho phép khách xuống tàu.");
        }

        if (boarding.Status is "Skipped" || disembarking.Status is "Skipped")
        {
            throw new ArgumentException("Bến lên hoặc bến xuống được chọn không được phục vụ trên chuyến này.");
        }
    }

    /// <summary>
    /// BR-FARE-01/02: chỉ TripType và SeatClass làm thay đổi đơn giá. EffectiveFrom/To chỉ dùng
    /// quản lý phiên bản bảng giá do Admin cấu hình, KHÔNG phải giá động theo giờ chạy.
    /// </summary>
    private Task<List<FareRule>> LoadFaresAsync(TripType tripType, DateTimeOffset now, CancellationToken cancellationToken) =>
        _context.FareRules
            .AsNoTracking()
            .Where(f => f.TripType == tripType.ToString()
                        && f.IsActive
                        && f.EffectiveFrom <= now
                        && (f.EffectiveTo == null || f.EffectiveTo > now))
            .ToListAsync(cancellationToken);

    /// <summary>
    /// BR-COM-05/06: mỗi ghế cần đúng một hành khách. Nếu client không khai báo danh sách riêng thì
    /// mặc định người mua là người đi, vẫn tạo đủ PassengerName bắt buộc trên từng SeatReservation.
    /// </summary>
    private static List<PassengerInput> ResolvePassengers(CreateBookingCommand request)
    {
        var seatCount = request.SeatIds.Distinct().Count();

        if (request.Passengers is null || request.Passengers.Count == 0)
        {
            return Enumerable.Repeat(
                new PassengerInput(request.CustomerName, request.CustomerEmail, request.CustomerPhone),
                seatCount).ToList();
        }

        return request.Passengers.Count == seatCount
            ? request.Passengers.ToList()
            : throw Validation("Danh sách hành khách phải khớp với số ghế đã chọn.");
    }

    private static string ToServiceType(TripType tripType) =>
        tripType == TripType.Sightseeing ? "Sightseeing" : "Regular";

    private static ValidationException Validation(string message) =>
        new(new List<FluentValidation.Results.ValidationFailure> { new(string.Empty, message) });
}