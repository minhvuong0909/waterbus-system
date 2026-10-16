using MediatR;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Bookings.Services;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;

namespace WaterbusSystem.Application.Features.Bookings.Queries.GetCheckout;

public record CheckoutTicketDto(Guid TicketId, string TicketCode, string PassengerName, string SeatCode,
    string Status, string BoardingStatus, decimal PaidFare);
public record CheckoutBookingDto(Guid BookingId, string BookingCode, string PublicBookingId, Guid TripId,
    Guid BoardingCallId, Guid DisembarkingCallId, string Status, decimal QuotedTotal,
    IReadOnlyList<CheckoutTicketDto> Tickets);
public record CheckoutDetailDto(Guid OrderId, string Status, decimal QuotedTotal, DateTimeOffset? ExpiresAt,
    IReadOnlyList<CheckoutBookingDto> Bookings, IReadOnlyList<CheckoutPaymentDto> Payments);
public record CheckoutPaymentDto(Guid TransactionId, string Type, string Status, decimal Amount, bool RequiresReconciliation);
public record GetCheckoutQuery(Guid OrderId, string? AccessToken) : IRequest<CheckoutDetailDto>;
public sealed class GetCheckoutQueryHandler(IApplicationDbContext context, CheckoutAccessService access) : IRequestHandler<GetCheckoutQuery, CheckoutDetailDto>
{
    public async Task<CheckoutDetailDto> Handle(GetCheckoutQuery request, CancellationToken ct)
    {
        await access.RequireAsync(request.OrderId, request.AccessToken, ct);
        var order = await context.PurchaseOrders.AsNoTracking().Include(o => o.Bookings).ThenInclude(b => b.Tickets).ThenInclude(t => t.SeatReservation)
            .Include(o => o.FinancialTransactions).FirstOrDefaultAsync(o => o.Id == request.OrderId, ct)
            ?? throw new NotFoundException(nameof(PurchaseOrder), request.OrderId);
        return new CheckoutDetailDto(order.Id, order.Status.ToString(), order.QuotedTotal, order.ExpiresAt,
            order.Bookings.Select(b => new CheckoutBookingDto(b.Id, b.BookingCode, b.PublicBookingId, b.TripId,
                b.BoardingCallId, b.DisembarkingCallId, b.Status.ToString(), b.QuotedTotal,
                b.Tickets.Select(t => new CheckoutTicketDto(t.Id, t.TicketCode, t.SeatReservation!.PassengerName,
                    t.SeatReservation.SeatCodeAtSale ?? "", t.Status.ToString(), t.BoardingStatus.ToString(), t.PaidFareSnapshot)).ToList())).ToList(),
            order.FinancialTransactions.Select(p => new CheckoutPaymentDto(p.Id, p.TransactionType.ToString(),
                p.Status.ToString(), p.Amount, p.FailureReason != null && p.FailureReason.StartsWith("ReconciliationRequired:"))).ToList());
    }
}
