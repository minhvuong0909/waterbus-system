using MediatR;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Bookings.Services;
using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Application.Features.Bookings.Commands.ExpireCheckoutHolds;

public record ExpireCheckoutHoldsCommand : IRequest<int>;
public sealed class ExpireCheckoutHoldsCommandHandler(IApplicationDbContext context,
    ICommercialTransactionRunner transactions, CheckoutOptions options,
    ISeatEventPublisher? events = null, ICheckoutSessionService? sessions = null) : IRequestHandler<ExpireCheckoutHoldsCommand, int>
{
    public async Task<int> Handle(ExpireCheckoutHoldsCommand request, CancellationToken ct)
    {
        var expired = await transactions.RunAsync(async () =>
        {
            var now = DateTimeOffset.UtcNow;
            var legacyDeadline = now.Subtract(options.HoldDuration);
            var orders = await context.PurchaseOrders.Include(o => o.Bookings).ThenInclude(b => b.SeatReservations)
                .Where(o => (o.Status == OrderStatus.PendingPayment || o.Status == OrderStatus.Draft) &&
                    (o.ExpiresAt <= now || (o.ExpiresAt == null && o.CreatedAt < legacyDeadline))).ToListAsync(ct);
            foreach (var order in orders)
            {
                order.Status = OrderStatus.Expired;
                foreach (var booking in order.Bookings.Where(b => b.Status == BookingStatus.PendingPayment || b.Status == BookingStatus.Draft))
                {
                    booking.Status = BookingStatus.Cancelled;
                    foreach (var r in booking.SeatReservations.Where(r => r.Status == ReservationStatus.Pending))
                        r.Status = ReservationStatus.Cancelled;
                }
            }
            await context.SaveChangesAsync(ct);
            return orders;
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
