using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Infrastructure.Persistence;

namespace WaterbusSystem.Infrastructure.BackgroundJobs;

/// <summary>
/// BackgroundService quét định kỳ và hủy các Booking đang giữ chỗ (Pending) quá 10 phút mà
/// chưa thanh toán, giải phóng ghế (Ticket/SeatReservation -> Cancelled) để hệ thống không bị
/// "kẹt ghế ảo" vĩnh viễn khi khách bỏ ngang quy trình đặt vé.
///
/// Khóa Redis RedLock tương ứng đã có TTL 10 phút nên tự hết hạn song song, không cần giải phóng thủ công ở đây.
/// </summary>
public class ExpiredBookingCleanupService : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan HoldDuration = TimeSpan.FromMinutes(10);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExpiredBookingCleanupService> _logger;

    public ExpiredBookingCleanupService(IServiceScopeFactory scopeFactory, ILogger<ExpiredBookingCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupExpiredBookingsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CLEANUP ERROR] Lỗi khi dọn dẹp Booking hết hạn giữ chỗ.");
            }

            try
            {
                await Task.Delay(PollingInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Dừng service bình thường khi ứng dụng shutdown
            }
        }
    }

    private async Task CleanupExpiredBookingsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var deadline = DateTimeOffset.UtcNow - HoldDuration;

        var expiredBookings = await context.Bookings
            .Where(b => !b.IsDeleted && b.Status == BookingStatus.Pending && b.CreatedAt < deadline)
            .ToListAsync(cancellationToken);

        if (expiredBookings.Count == 0)
        {
            return;
        }

        var expiredBookingIds = expiredBookings.Select(b => b.Id).ToList();

        var expiredReservations = await context.SeatReservations
            .Where(r => !r.IsDeleted && expiredBookingIds.Contains(r.BookingId)
                        && r.Status == ReservationStatus.Pending)
            .ToListAsync(cancellationToken);

        foreach (var booking in expiredBookings)
        {
            // Phòng trường hợp IPN VNPAY xử lý thành công gần đúng lúc job quét chạy (race condition):
            // chỉ huỷ nếu vẫn còn Pending tại thời điểm ghi, RowVersion sẽ tự phát hiện xung đột nếu có.
            booking.Status = BookingStatus.Cancelled;
            booking.PaymentStatus = PaymentStatus.Failed;
            booking.UpdatedAt = DateTimeOffset.UtcNow;
        }

        foreach (var reservation in expiredReservations)
        {
            reservation.Status = ReservationStatus.Cancelled;
            reservation.UpdatedAt = DateTimeOffset.UtcNow;
        }

        try
        {
            var affected = await context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "[CLEANUP] Đã huỷ {Count} Booking hết hạn giữ chỗ (>10 phút), ảnh hưởng {Affected} bản ghi.",
                expiredBookings.Count, affected);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Một hoặc nhiều Booking đã được IPN VNPAY cập nhật đồng thời (ví dụ vừa Confirmed) ->
            // bỏ qua, không làm chết job, lần quét kế tiếp sẽ tự bỏ qua các booking đã không còn Pending.
            _logger.LogWarning(ex,
                "[CLEANUP] Xung đột concurrency khi huỷ booking hết hạn (có thể do IPN xử lý gần đồng thời). Bỏ qua, chờ lần quét sau.");
        }
    }
}
