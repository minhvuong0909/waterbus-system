using MediatR;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Bookings.Services;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;

namespace WaterbusSystem.Application.Features.Bookings.Commands.CancelCheckout;

public record CancelCheckoutCommand(Guid OrderId, string? AccessToken) : IRequest<bool>;
public sealed class CancelCheckoutCommandHandler(IApplicationDbContext context, ICommercialTransactionRunner transactions,
    CheckoutAccessService access, ISeatEventPublisher? events = null, ICheckoutSessionService? sessions = null) : IRequestHandler<CancelCheckoutCommand, bool>
{
    public async Task<bool> Handle(CancelCheckoutCommand request, CancellationToken ct)
    {
        await access.RequireAsync(request.OrderId, request.AccessToken, ct);
        var released = await transactions.RunAsync(async () =>
        {
            var order = await context.PurchaseOrders.Include(o => o.Bookings).ThenInclude(b => b.SeatReservations)
                .FirstOrDefaultAsync(o => o.Id == request.OrderId, ct) ?? throw new NotFoundException(nameof(PurchaseOrder), request.OrderId);
            if (order.Status == OrderStatus.Confirmed) throw new ConcurrencyException("Đơn đã thanh toán không thể bỏ giữ ghế.");
            var holds = order.Bookings.SelectMany(b => b.SeatReservations).Where(r => r.Status == ReservationStatus.Pending).ToList();
            order.Status = OrderStatus.Cancelled;
            foreach (var b in order.Bookings.Where(b => b.Status == BookingStatus.PendingPayment || b.Status == BookingStatus.Draft)) b.Status = BookingStatus.Cancelled;
            foreach (var r in holds) r.Status = ReservationStatus.Cancelled;
            await context.SaveChangesAsync(ct);
            return holds;
        }, ct);
        await CheckoutSeatEvents.PublishAsync(events, released, "Released", null, ct);
        if (sessions is not null)
            try { await sessions.ReleaseAsync("order:" + request.OrderId, ct); } catch (Exception) { }
        return true;
    }
}
