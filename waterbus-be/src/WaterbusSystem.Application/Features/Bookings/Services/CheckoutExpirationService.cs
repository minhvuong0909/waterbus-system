using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Interfaces.Persistence;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Application.Features.Bookings.Services;

public sealed class CheckoutExpirationService(IOrderRepository orders, IUnitOfWork unitOfWork,
    ICommercialTransactionRunner transactions, CheckoutOptions options,
    ISeatEventPublisher? events = null, ICheckoutSessionService? sessions = null) : ICheckoutExpirationService
{
    public async Task<int> ExpireAsync(CancellationToken ct = default)
    {
        var expired = await transactions.RunAsync(async () =>
        {
            var now = DateTimeOffset.UtcNow;
            var legacyDeadline = now.Subtract(options.HoldDuration);
            var expiredOrders = await orders.GetExpiredAsync(now, legacyDeadline, ct);
            foreach (var order in expiredOrders)
            {
                order.Status = OrderStatus.Expired;
                foreach (var booking in order.Bookings.Where(b => b.Status == BookingStatus.PendingPayment || b.Status == BookingStatus.Draft))
                {
                    booking.Status = BookingStatus.Cancelled;
                    foreach (var r in booking.SeatReservations.Where(r => r.Status == ReservationStatus.Pending))
                        r.Status = ReservationStatus.Cancelled;
                }
            }
            await unitOfWork.SaveChangesAsync(ct);
            return expiredOrders;
        }, ct);
        foreach (var order in expired)
        {
            await CheckoutSeatEvents.PublishAsync(events, order.Bookings.SelectMany(b => b.SeatReservations)
                .Where(r => r.Status == ReservationStatus.Cancelled), "Released", null, ct);
            if (sessions is not null)
                try { await sessions.ReleaseAsync("order:" + order.Id, ct); } catch (Exception) { }
        }
        return expired.Count;
    }
}
