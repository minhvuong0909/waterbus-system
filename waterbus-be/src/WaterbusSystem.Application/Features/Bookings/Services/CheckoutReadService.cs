using WaterbusSystem.Application.DTOs.Responses.Bookings;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Application.Interfaces.Services;

namespace WaterbusSystem.Application.Features.Bookings.Services;

public sealed class CheckoutReadService(IOrderRepository orders, ICheckoutAccessService access) : ICheckoutReadService
{
    public async Task<CheckoutDetailDto> GetAsync(Guid orderId, string? accessToken, CancellationToken ct = default)
    {
        await access.RequireAsync(orderId, accessToken, ct);
        var order = await orders.GetDetailAsync(orderId, ct);
        return new CheckoutDetailDto(order.Id, order.Status.ToString(), order.QuotedTotal, order.ExpiresAt,
            order.Bookings.Select(b => new CheckoutBookingDto(b.Id, b.BookingCode, b.PublicBookingId, b.TripId,
                b.BoardingCallId, b.DisembarkingCallId, b.Status.ToString(), b.QuotedTotal,
                b.Tickets.Select(t => new CheckoutTicketDto(t.Id, t.TicketCode, t.SeatReservation!.PassengerName,
                    t.SeatReservation.SeatCodeAtSale ?? "", t.Status.ToString(), t.BoardingStatus.ToString(), t.PaidFareSnapshot)).ToList())).ToList(),
            order.FinancialTransactions.Select(p => new CheckoutPaymentDto(p.Id, p.TransactionType.ToString(),
                p.Status.ToString(), p.Amount, p.FailureReason != null && p.FailureReason.StartsWith("ReconciliationRequired:"))).ToList());
    }
}
