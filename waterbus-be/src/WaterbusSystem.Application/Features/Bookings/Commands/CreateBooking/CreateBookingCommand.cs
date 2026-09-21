using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
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
/// Command yêu cầu tạo đơn đặt và giữ ghế tạm thời trong 10 phút
/// </summary>
public record CreateBookingCommand(
    Guid TripId,
    List<Guid> SeatIds,
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

        RuleFor(x => x.SeatIds)
            .NotEmpty().WithMessage("Bạn phải chọn ít nhất một ghế.")
            .Must(seats => seats.Count <= 10).WithMessage("Mỗi lượt đặt tối đa 10 ghế.");

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
/// - Lớp 1 (RAM/Cache): Redis RedLock khóa tài nguyên ghế tạm thời
/// - Lớp 2 (Database): EF Core RowVersion Optimistic Concurrency
/// </summary>
public class CreateBookingCommandHandler : IRequestHandler<CreateBookingCommand, BookingResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IDistributedLockService _lockService;

    public CreateBookingCommandHandler(IApplicationDbContext context, IDistributedLockService lockService)
    {
        _context = context;
        _lockService = lockService;
    }

    public async Task<BookingResponseDto> Handle(CreateBookingCommand request, CancellationToken cancellationToken)
    {
        // =========================================================================
        // LỚP PHÒNG THỦ 1: REDIS REDLOCK (Khóa phân tán ở tầng Cache/RAM)
        // Ngăn chặn 2 khách hàng gửi request giữ cùng một ghế trong cùng 1 thời điểm
        // =========================================================================
        var acquiredLocks = new List<IAsyncDisposable>();

        try
        {
            foreach (var seatId in request.SeatIds)
            {
                var lockKey = $"lock:trip:{request.TripId}:seat:{seatId}";

                // Thử lấy lock trong 2 giây, TTL giữ chỗ 10 phút (600 giây), retry mỗi 100ms
                var lockHandle = await _lockService.AcquireLockAsync(
                    lockKey,
                    expiryTime: TimeSpan.FromMinutes(10),
                    waitTime: TimeSpan.FromSeconds(2),
                    retryTime: TimeSpan.FromMilliseconds(100),
                    cancellationToken);

                if (lockHandle == null)
                {
                    // Lớp 1 kích hoạt: Ghế đang bị giữ bởi phiên khác, dừng ngay không cho chạm CSDL
                    throw new SeatAlreadyBookedException(seatId.ToString());
                }

                acquiredLocks.Add(lockHandle);
            }

            // =========================================================================
            // LỚP PHÒNG THỦ 2: EF CORE OPTIMISTIC CONCURRENCY (Kiểm tra và lưu vào CSDL)
            // =========================================================================
            var trip = await _context.Trips
                .Include(t => t.Route)
                .FirstOrDefaultAsync(t => t.Id == request.TripId && !t.IsDeleted, cancellationToken)
                ?? throw new NotFoundException(nameof(Trip), request.TripId);

            // Kiểm tra ghế đã tồn tại vé hợp lệ hoặc đã bán chưa
            var soldTickets = await _context.Tickets
                .Where(t => t.TripId == request.TripId && 
                            request.SeatIds.Contains(t.SeatId) && 
                            (t.Status == TicketStatus.Valid || t.Status == TicketStatus.CheckedIn))
                .ToListAsync(cancellationToken);

            if (soldTickets.Count != 0)
            {
                throw new ConcurrencyException("Một trong các ghế bạn chọn đã được bán trước đó!");
            }

            // Tạo đối tượng Booking và danh sách vé Pending
            var booking = new Booking
            {
                BookingCode = $"WB{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(100, 999)}",
                CustomerName = request.CustomerName,
                CustomerEmail = request.CustomerEmail,
                CustomerPhone = request.CustomerPhone,
                Status = BookingStatus.Pending,
                PaymentStatus = PaymentStatus.Pending,
                TotalAmount = trip.BasePrice * request.SeatIds.Count
            };

            foreach (var seatId in request.SeatIds)
            {
                booking.Tickets.Add(new Ticket
                {
                    TripId = request.TripId,
                    SeatId = seatId,
                    TicketCode = $"TK{Random.Shared.Next(10000000, 99999999)}",
                    Price = trip.BasePrice,
                    PassengerName = request.CustomerName,
                    Status = TicketStatus.Pending
                });
            }

            _context.Bookings.Add(booking);

            // Lưu CSDL: Nếu 2 luồng cùng ghi đè, SQL Server sẽ phát hiện RowVersion sai lệch
            // và ném ra DbUpdateConcurrencyException tại tầng CSDL
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
            // Lớp 2 kích hoạt: Giải phóng lock và ném lỗi xung đột dữ liệu
            throw new ConcurrencyException("Xung đột dữ liệu khi cập nhật ghế. Vui lòng thử lại!", ex);
        }
        catch (Exception)
        {
            // Trong trường hợp xảy ra bất kỳ lỗi logic nào, giải phóng toàn bộ Redis Lock đã acquire
            foreach (var acquiredLock in acquiredLocks)
            {
                await acquiredLock.DisposeAsync();
            }
            throw;
        }
    }
}
