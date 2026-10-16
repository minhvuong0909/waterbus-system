using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Application.Features.Bookings.Services;

/// <summary>Đồng bộ cache và event sau commit, từ snapshot đã lưu của toàn Order.</summary>
public sealed class CheckoutCommitEffects(IApplicationDbContext context, ICheckoutSessionService sessions,
    ILogger<CheckoutCommitEffects> logger, ISeatEventPublisher? events = null)
{
    public async Task HeldAsync(Guid orderId, CancellationToken ct)
    {
        var order = await context.PurchaseOrders.AsNoTracking().FirstAsync(o => o.Id == orderId, ct);
        if (order.Status is not (OrderStatus.Draft or OrderStatus.PendingPayment) ||
            order.ExpiresAt is null || order.ExpiresAt <= DateTimeOffset.UtcNow) return;
        var reservations = await context.SeatReservations.AsNoTracking()
            .Where(r => r.Booking!.OrderId == orderId && r.Status == ReservationStatus.Pending).ToListAsync(ct);
        try
        {
            await sessions.HoldUntilAsync("order:" + orderId, orderId,
                reservations.Select(r => new SeatHold(r.TripId, r.SeatId, r.BoardingStopOrder, r.DisembarkingStopOrder)).ToList(),
                order.ExpiresAt.Value, ct);
        }
        catch (Exception)
        {
            // Không trả lỗi tạo đơn sau khi DB đã commit; client dùng deadline của DB.
            logger.LogWarning("Không đồng bộ được cache checkout; DB vẫn giữ reservation và deadline.");
        }
        await CheckoutSeatEvents.PublishAsync(events, reservations, "Held", order.ExpiresAt, ct);
    }
}
