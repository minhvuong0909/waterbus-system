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
/// DTO trả về sau khi tạo đơn đặt và giữ chỗ thành công
/// </summary>
public record BookingResponseDto(
    Guid BookingId,
    string BookingCode,
    decimal TotalAmount,
    string Status,
    int HoldDurationSeconds);

/// <summary>
/// Command yêu cầu tạo đơn đặt và giữ ghế tạm thời trong 10 phút.
/// Hỗ trợ bán vé theo chặng (Segment-based): khách chỉ giữ chỗ cho đoạn [BoardingStationId, DisembarkingStationId),
/// cho phép cùng 1 ghế được bán lại cho hành khách khác ở đoạn chặng không giao thoa trên cùng 1 chuyến.
/// </summary>
public record CreateBookingCommand(
    Guid TripId,
    List<Guid> SeatIds,
    Guid BoardingStationId,
    Guid DisembarkingStationId,
    string CustomerName,
    string CustomerEmail,
    string CustomerPhone) : IRequest<BookingResponseDto>;

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
            .NotEqual(x => x.BoardingStationId).WithMessage("Bến lên và bến xuống không được trùng nhau.");

        RuleFor(x => x.SeatIds)
            .NotEmpty().WithMessage("Bạn phải chọn ít nhất một ghế.")
            .Must(seats => seats.Count <= 10).WithMessage("Mỗi lượt đặt tối đa 10 ghế.")
            .Must(seats => seats.Distinct().Count() == seats.Count)
                .WithMessage("Danh sách ghế không được chứa ghế trùng lặp.");

        RuleFor(x => x.CustomerName)
            .NotEmpty().WithMessage("Họ và tên khách hàng không được để trống.")
            .MaximumLength(150).WithMessage("Họ tên không được vượt quá 150 ký tự.");

        RuleFor(x => x.CustomerEmail)
            .NotEmpty().WithMessage("Email không được để trống.")
            .EmailAddress().WithMessage("Định dạng email không hợp lệ.");

        RuleFor(x => x.CustomerPhone)
            .NotEmpty().WithMessage("Số điện thoại không được để trống.")
            .Matches(@"^[0-9]{10,11}$").WithMessage("Số điện thoại phải từ 10 đến 11 chữ số.");
    }
}

/// <summary>
/// Handler xử lý nghiệp vụ đặt vé với 2 LỚP PHÒNG THỦ CHỐNG DOUBLE-BOOKING / OVERSELLING:
/// - Lớp 1 (RAM/Cache): Redis RedLock khóa tài nguyên ghế tạm thời (toàn chuyến, không phân biệt chặng -
///   đơn giản và an toàn hơn so với khóa theo từng chặng, đổi lại concurrency thấp hơn một chút)
/// - Lớp 2 (Database): Thuật toán kiểm tra đụng độ chặng (Segment Overlap) qua ISeatAvailabilityService
///   dựa trên bảng SeatReservation, cho phép bán lại ghế nhiều lần trên cùng 1 chuyến nếu khác chặng
/// </summary>
public class CreateBookingCommandHandler : IRequestHandler<CreateBookingCommand, BookingResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IDistributedLockService _lockService;
    private readonly ISeatAvailabilityService _seatAvailabilityService;

    public CreateBookingCommandHandler(
        IApplicationDbContext context,
        IDistributedLockService lockService,
        ISeatAvailabilityService seatAvailabilityService)
    {
        _context = context;
        _lockService = lockService;
        _seatAvailabilityService = seatAvailabilityService;
    }

    public async Task<BookingResponseDto> Handle(CreateBookingCommand request, CancellationToken cancellationToken)
    {
        // =========================================================================
        // LỚP PHÒNG THỦ 1: REDIS REDLOCK (Khóa phân tán ở tầng Cache/RAM)
        // Đây CHỈ là mutex ngắn hạn bảo vệ đoạn "kiểm tra đụng độ chặng rồi ghi DB" (critical section)
        // khỏi race condition khi 2 request cùng seat đến gần như đồng thời - KHÔNG phải cơ chế giữ chỗ
        // 10 phút (việc đó do Booking.CreatedAt + ExpiredBookingCleanupService + segment-overlap đảm nhiệm).
        // Vì vậy lock luôn được giải phóng ngay sau khối try (finally), và TTL chỉ cần đủ ngắn để phòng
        // trường hợp tiến trình crash giữa chừng, KHÔNG đặt bằng đúng 10 phút như trước đây - nếu không,
        // ghế sẽ bị khóa cứng toàn bộ 10 phút cho MỌI chặng khác dù không hề giao thoa, triệt tiêu tác dụng
        // của tính năng bán lại ghế theo chặng (Segment Overlap).
        // =========================================================================
        var acquiredLocks = new List<IAsyncDisposable>();

        try
        {
            foreach (var seatId in request.SeatIds)
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

            // =========================================================================
            // Nạp Trip kèm Route + 2 đầu bến để xác định chiều di chuyển
            // =========================================================================
            var trip = await _context.Trips
                .Include(t => t.Route!).ThenInclude(r => r.DepartureStation)
                .Include(t => t.Route!).ThenInclude(r => r.ArrivalStation)
                .FirstOrDefaultAsync(t => t.Id == request.TripId && !t.IsDeleted, cancellationToken)
                ?? throw new NotFoundException(nameof(Trip), request.TripId);

            // Tàu phải được phân công trước khi mở bán
            if (trip.BoatId == null)
            {
                throw new ValidationException(new List<FluentValidation.Results.ValidationFailure>
                {
                    new(nameof(request.TripId), "Chuyến tàu chưa được phân công tàu. Không thể đặt vé.")
                });
            }

            var route = trip.Route
                ?? throw new NotFoundException(nameof(Domain.Entities.Route), trip.RouteId);
            var departureStation = route.DepartureStation
                ?? throw new NotFoundException(nameof(Station), route.DepartureStationId);
            var arrivalStation = route.ArrivalStation
                ?? throw new NotFoundException(nameof(Station), route.ArrivalStationId);

            // =========================================================================
            // Xác định chặng [BoardingStopOrder, DisembarkingStopOrder) theo chiều di chuyển thực tế của Trip.
            // GIẢ ĐỊNH KIẾN TRÚC: hệ thống hiện chưa có entity mô tả danh sách bến trung gian theo thứ tự
            // riêng cho từng Route/Trip (không có RouteStop/TripStop). Do đó ta dùng Station.OrderIndex
            // (thứ tự toàn cục dọc tuyến sông duy nhất) làm StopOrder, với điều kiện: Trip được xem là
            // dừng tuần tự ở MỌI bến có OrderIndex nằm giữa Departure và Arrival của Route.
            // Nếu sau này hệ thống có nhiều tuyến sông độc lập không cùng 1 trục OrderIndex, cần bổ sung
            // entity RouteStop(RouteId, StationId, StopOrder) và thay thế toàn bộ đoạn tính toán dưới đây.
            // =========================================================================
            var boardingStation = await _context.Stations
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == request.BoardingStationId && !s.IsDeleted, cancellationToken)
                ?? throw new NotFoundException(nameof(Station), request.BoardingStationId);

            var disembarkingStation = await _context.Stations
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == request.DisembarkingStationId && !s.IsDeleted, cancellationToken)
                ?? throw new NotFoundException(nameof(Station), request.DisembarkingStationId);

            var directionSign = arrivalStation.OrderIndex >= departureStation.OrderIndex ? 1 : -1;
            var minOrder = Math.Min(departureStation.OrderIndex, arrivalStation.OrderIndex);
            var maxOrder = Math.Max(departureStation.OrderIndex, arrivalStation.OrderIndex);

            if (boardingStation.OrderIndex < minOrder || boardingStation.OrderIndex > maxOrder ||
                disembarkingStation.OrderIndex < minOrder || disembarkingStation.OrderIndex > maxOrder)
            {
                throw new ValidationException(new List<FluentValidation.Results.ValidationFailure>
                {
                    new(nameof(request.BoardingStationId), "Bến lên/xuống không nằm trên tuyến của chuyến tàu này.")
                });
            }

            var boardingStopOrder = directionSign * boardingStation.OrderIndex;
            var disembarkingStopOrder = directionSign * disembarkingStation.OrderIndex;

            if (boardingStopOrder >= disembarkingStopOrder)
            {
                throw new ValidationException(new List<FluentValidation.Results.ValidationFailure>
                {
                    new(nameof(request.DisembarkingStationId), "Bến xuống phải nằm sau bến lên theo chiều di chuyển của chuyến tàu.")
                });
            }

            // =========================================================================
            // Kiểm tra toàn bộ SeatId thực sự tồn tại và thuộc đúng con tàu (BoatId) của chuyến này.
            // Ngăn chặn việc gửi SeatId hợp lệ nhưng thuộc một tàu khác.
            // =========================================================================
            var requestedSeatIds = request.SeatIds.Distinct().ToList();

            var seats = await _context.Seats
                .Where(s => !s.IsDeleted && requestedSeatIds.Contains(s.Id))
                .ToListAsync(cancellationToken);

            if (seats.Count != requestedSeatIds.Count)
            {
                throw new NotFoundException(nameof(Seat), string.Join(",", requestedSeatIds));
            }

            var wrongBoatSeats = seats.Where(s => s.BoatId != trip.BoatId).ToList();
            if (wrongBoatSeats.Count != 0)
            {
                throw new ValidationException(new List<FluentValidation.Results.ValidationFailure>
                {
                    new(nameof(request.SeatIds),
                        $"Ghế '{string.Join(", ", wrongBoatSeats.Select(s => s.SeatCode))}' không thuộc tàu vận hành chuyến này.")
                });
            }

            // =========================================================================
            // LỚP PHÒNG THỦ 2: KIỂM TRA ĐỤNG ĐỘ CHẶNG (SEGMENT OVERLAP) TẠI CSDL
            // =========================================================================
            var availableSeatIds = await _seatAvailabilityService.GetAvailableSeatsAsync(
                request.TripId, requestedSeatIds, boardingStopOrder, disembarkingStopOrder, cancellationToken);

            var unavailableSeatIds = requestedSeatIds.Except(availableSeatIds).ToList();
            if (unavailableSeatIds.Count != 0)
            {
                throw new ConcurrencyException("Một hoặc nhiều ghế bạn chọn đã được đặt trên đoạn chặng này!");
            }

            // =========================================================================
            // Tạo Booking + Tickets + SeatReservations (giữ chỗ theo chặng)
            // =========================================================================
            var now = DateTimeOffset.UtcNow;

            // Tìm giá vé hiện hành theo TripType × SeatClass
            var tripTypeStr = trip.TripType.ToString();

            // Load tất cả FareRule cần thiết một lần
            var seatClassIds = seats.Select(s => s.SeatClassId).Distinct().ToList();
            var fareRules = await _context.FareRules
                .Where(f => f.TripType == tripTypeStr
                         && seatClassIds.Contains(f.SeatClassId)
                         && f.IsActive
                         && f.EffectiveFrom <= now
                         && (f.EffectiveTo == null || f.EffectiveTo > now))
                .ToListAsync(cancellationToken);

            // Helper để lấy giá theo SeatClassId
            decimal GetFare(Guid seatClassId)
            {
                var rule = fareRules
                    .Where(f => f.SeatClassId == seatClassId)
                    .OrderByDescending(f => f.EffectiveFrom)
                    .FirstOrDefault()
                    ?? throw new ValidationException(new List<FluentValidation.Results.ValidationFailure>
                    {
                        new("FareRule", $"Không tìm thấy bảng giá cho loại chuyến {tripTypeStr} và hạng ghế này.")
                    });
                return rule.Price;
            }

            var order = new PurchaseOrder
            {
                PurchaserName  = request.CustomerName,
                PurchaserEmail = request.CustomerEmail,
                PurchaserPhone = request.CustomerPhone,
                PurchaseMode   = "OneWay",
                Status         = "Pending",
                QuotedTotal    = seats.Sum(s => GetFare(s.SeatClassId)),
                ExpiresAt      = now.AddMinutes(10)
            };

            var booking = new Booking
            {
                OrderId         = order.Id,
                BookingCode     = CodeGenerator.GenerateBookingCode(now),
                PublicBookingId = Guid.NewGuid().ToString("N")[..16].ToUpperInvariant(),
                CustomerName    = request.CustomerName,
                CustomerEmail   = request.CustomerEmail,
                CustomerPhone   = request.CustomerPhone,
                Status          = BookingStatus.Pending,
                PaymentStatus   = PaymentStatus.Pending,
                TotalAmount     = order.QuotedTotal
            };

            order.Bookings.Add(booking);
            _context.PurchaseOrders.Add(order);

            foreach (var seat in seats)
            {
                _context.SeatReservations.Add(new SeatReservation
                {
                    TripId = request.TripId,
                    SeatId = seat.Id,
                    BookingId = booking.Id,
                    BoardingStopOrder = boardingStopOrder,
                    DisembarkingStopOrder = disembarkingStopOrder,
                    Status = ReservationStatus.Pending
                });
            }


            await _context.SaveChangesAsync(cancellationToken);

            return new BookingResponseDto(
                booking.Id,
                booking.BookingCode,
                booking.TotalAmount,
                booking.Status.ToString(),
                HoldDurationSeconds: 600);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyException("Xung đột dữ liệu khi cập nhật ghế. Vui lòng thử lại!", ex);
        }
        finally
        {
            // Luôn giải phóng lock ngay khi kết thúc critical section, dù thành công hay thất bại -
            // lock chỉ bảo vệ đoạn kiểm tra + ghi DB, không phải cơ chế giữ chỗ 10 phút (xem ghi chú ở trên).
            foreach (var acquiredLock in acquiredLocks)
            {
                await acquiredLock.DisposeAsync();
            }
        }
    }
}
