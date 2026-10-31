using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Responses.Bookings;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Application.Interfaces.Persistence;
using WaterbusSystem.Domain.Common;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;
using ValidationException = WaterbusSystem.Application.Common.Exceptions.ValidationException;

namespace WaterbusSystem.Application.Features.Bookings.Services;

public sealed class BookingCreationService
{
    private readonly IOrderRepository _orders;
    private readonly IBookingRepository _bookings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBookingReferenceRepository _references;
    private readonly IDistributedLockService _lockService;
    private readonly ISeatAvailabilityService _seatAvailabilityService;
    private readonly ICheckoutAccessService _access;
    private readonly TimeSpan _holdDuration;

    private static readonly TripStatus[] SellableTripStatuses =
    [
        TripStatus.Scheduled,
        TripStatus.Boarding
    ];

    public BookingCreationService(
        IOrderRepository orders,
        IBookingRepository bookings,
        IUnitOfWork unitOfWork,
        IBookingReferenceRepository references,
        IDistributedLockService lockService,
        ISeatAvailabilityService seatAvailabilityService,
        CheckoutOptions options,
        ICheckoutAccessService access)
    {
        _orders = orders;
        _bookings = bookings;
        _unitOfWork = unitOfWork;
        _references = references;
        _lockService = lockService;
        _seatAvailabilityService = seatAvailabilityService;
        _access = access;
        _holdDuration = options.HoldDuration;
    }


    public async Task<BookingResponse> CreateAsync(
        BookingCreationInput request,
        CancellationToken cancellationToken,
        PurchaseOrder? sharedOrder = null,
        bool draft = false)
    {
        var acquiredLocks = new List<IAsyncDisposable>();

        try
        {
            var requestedSeatIds = request.SeatIds.Distinct().ToList();

            foreach (var seatId in requestedSeatIds.OrderBy(id => id))
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

            var trip = await _references.GetTripWithStopsAsync(
                            request.TripId,
                            cancellationToken)
                            ?? throw new NotFoundException(nameof(Trip), request.TripId);

            EnsureTripIsSellable(trip, now);

            if (trip.Route == null)
            {
                throw new NotFoundException(nameof(Route), trip.RouteId);
            }

            if (!string.Equals(trip.Route.ServiceType, ToServiceType(trip.TripType), StringComparison.OrdinalIgnoreCase))
            {
                throw Validation("Loại dịch vụ của chuyến không khớp với tuyến, không thể đặt vé.");
            }

            var (boardingCall, disembarkingCall) = ResolveJourneyCalls(
                trip, request.BoardingStationId, request.DisembarkingStationId,
                request.BoardingCallId, request.DisembarkingCallId);

            var boardingStopOrder = boardingCall.VisitOrder;
            var disembarkingStopOrder = disembarkingCall.VisitOrder;

            var seats = await _references.GetActiveSeatsAsync(
                requestedSeatIds,
                cancellationToken);

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

            var seatsWithoutClass = seats.Where(s => s.SeatClass is not { IsActive: true, IsDeleted: false }).ToList();
            if (seatsWithoutClass.Count != 0)
            {
                throw Validation(
                    $"Ghế '{string.Join(", ", seatsWithoutClass.Select(s => s.SeatCode))}' chưa có hạng ghế đang hoạt động nên chưa thể tính giá.");
            }

            var fares = await _references.GetEffectiveFaresAsync(
                trip.TripType,
                now,
                cancellationToken);
            if (fares.GroupBy(f => f.SeatClassId).Any(g => g.Count() != 1))
                throw Validation("Có nhiều bảng giá đồng thời cho cùng hạng ghế; cần Admin kiểm tra.");
            var fareBySeatClassId = fares.ToDictionary(f => f.SeatClassId, f => f);

            var missingFare = seats.FirstOrDefault(s => !fareBySeatClassId.ContainsKey(s.SeatClassId));
            if (missingFare != null)
            {
                throw Validation(
                    $"Chưa cấu hình giá vé cho hạng ghế '{missingFare.SeatClass!.Name}' trên chuyến {trip.TripType}. " +
                    "Vui lòng liên hệ quản trị viên.");
            }

            if (seats.Any(s => fareBySeatClassId[s.SeatClassId].Currency != "VND" ||
                fareBySeatClassId[s.SeatClassId].Price < 0))
                throw Validation("Giá vé phải không âm và sử dụng đơn vị VND.");

            var availableSeatIds = await _seatAvailabilityService.GetAvailableSeatsAsync(
                trip.Id, requestedSeatIds, boardingStopOrder, disembarkingStopOrder, cancellationToken);

            var unavailableSeatIds = requestedSeatIds.Except(availableSeatIds).ToList();
            if (unavailableSeatIds.Count != 0)
            {
                throw new ConcurrencyException("Một hoặc nhiều ghế bạn chọn đã được đặt trên đoạn chặng này!");
            }

            var passengers = ResolvePassengers(request);
            var order = sharedOrder ?? new PurchaseOrder
            {
                PurchaserName = request.CustomerName,
                PurchaserEmail = request.CustomerEmail,
                PurchaserPhone = request.CustomerPhone,
                PurchaseMode = PurchaseMode.OneWay,
                Status = draft ? OrderStatus.Draft : OrderStatus.PendingPayment,
                Currency = "VND",
                ExpiresAt = now.Add(_holdDuration)
            };

            var booking = new Booking
            {
                Order = order,
                OrderId = order.Id,
                BookingCode = CodeGenerator.GenerateBookingCode(now),
                CustomerName = request.CustomerName,
                CustomerEmail = request.CustomerEmail,
                CustomerPhone = request.CustomerPhone,
                Status = draft ? BookingStatus.Draft : BookingStatus.PendingPayment,
                PaymentStatus = PaymentStatus.Pending
            };
            booking.SetJourney(boardingCall, disembarkingCall);

            var seatById = seats.ToDictionary(s => s.Id);
            var orderedSeats = requestedSeatIds.Select(seatId => seatById[seatId]).ToList();

            var quotedTotal = 0m;
            for (var i = 0; i < orderedSeats.Count; i++)
            {
                var seat = orderedSeats[i];
                var fare = fareBySeatClassId[seat.SeatClassId].Price;
                var passenger = passengers[i];

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

            order.QuotedTotal += quotedTotal;
            booking.QuotedTotal = quotedTotal;

            order.Bookings.Add(booking);

            if (sharedOrder is not null) order.PurchaseMode = PurchaseMode.RoundTrip;
            if (!draft) order.ValidateForCheckout();

            string? accessToken = sharedOrder is null
                    ? _access.Issue(order, now)
                    : null;

            if (sharedOrder is null)
            {
                _orders.Add(order);
            }

            _bookings.Add(booking);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var remainingSeconds = Math.Max(0, (int)(order.ExpiresAt!.Value - DateTimeOffset.UtcNow).TotalSeconds);

            return new BookingResponse(
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
                CheckoutSessionId: "order:" + order.Id, AccessToken: accessToken);
        }
        finally
        {
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

        if (trip.ActualDepartureTime != null) throw Validation("Chuyến đã bắt đầu hành trình.");

        if (trip.BoatId == null)
        {
            throw Validation("Chuyến tàu chưa được phân công tàu nên chưa mở bán.");
        }

        if (trip.Boat is not { IsActive: true, IsDeleted: false })
            throw Validation("Tàu không còn hoạt động nên chưa thể đặt vé.");

        if (trip.SalesCloseAt.HasValue && trip.SalesCloseAt.Value <= now)
        {
            throw Validation("Chuyến tàu đã đóng bán vé.");
        }
    }

    internal static (TripStopCall Boarding, TripStopCall Disembarking) ResolveJourneyCalls(
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

        TripStopCall Resolve(Guid stationId, Guid? requestedCallId, bool isBoarding)
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

        var boardingCall = Resolve(boardingStationId, requestedBoardingCallId, isBoarding: true);
        var disembarkingCall = Resolve(disembarkingStationId, requestedDisembarkingCallId, isBoarding: false);

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

    private static List<BookingPassengerInput> ResolvePassengers(BookingCreationInput request)
    {
        var seatCount = request.SeatIds.Distinct().Count();

        if (request.Passengers is null || request.Passengers.Count == 0)
        {
            return Enumerable.Repeat(
                new BookingPassengerInput(
                    request.CustomerName,
                    request.CustomerEmail,
                    request.CustomerPhone),
                seatCount).ToList();
        }

        return request.Passengers.Count == seatCount
            ? request.Passengers.ToList()
            : throw Validation(
                "Danh sách hành khách phải khớp với số ghế đã chọn.");
    }


    private static string ToServiceType(TripType tripType) =>
        tripType == TripType.Sightseeing ? "Sightseeing" : "Regular";

    private static ValidationException Validation(string message) =>
        new(new List<FluentValidation.Results.ValidationFailure> { new(string.Empty, message) });
}
