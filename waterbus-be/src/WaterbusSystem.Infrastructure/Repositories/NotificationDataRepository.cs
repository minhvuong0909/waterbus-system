using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Features.Notifications;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Infrastructure.Persistence;

namespace WaterbusSystem.Infrastructure.Repositories;

/// <summary>Read-model chỉ đọc (AsNoTracking) cho thông báo email; chỉ dùng dữ liệu đã commit.</summary>
public sealed class NotificationDataRepository(ApplicationDbContext context) : INotificationDataRepository
{
    public Task<NotificationRecipient?> GetOrderRecipientAsync(Guid orderId, CancellationToken ct) =>
        context.PurchaseOrders.AsNoTracking().Where(o => o.Id == orderId)
            .Select(o => new NotificationRecipient(o.Id, null, o.PurchaserName, o.PurchaserEmail))
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<NotificationRecipient>> GetConfirmedBookingRecipientsForTripAsync(Guid tripId, CancellationToken ct) =>
        await context.Bookings.AsNoTracking()
            .Where(b => b.TripId == tripId && b.Status == BookingStatus.Confirmed && b.Tickets.Any())
            .Select(b => new NotificationRecipient(b.OrderId, b.Id, b.Order!.PurchaserName, b.Order.PurchaserEmail))
            .ToListAsync(ct);

    public Task<NotificationRecipient?> GetRefundRecipientAsync(Guid refundId, CancellationToken ct) =>
        context.FinancialTransactions.AsNoTracking()
            .Where(r => r.Id == refundId && r.TransactionType == FinancialTransactionType.Refund)
            .Select(r => new NotificationRecipient(r.OrderId, r.RefundBookingId, r.Order!.PurchaserName, r.Order.PurchaserEmail))
            .FirstOrDefaultAsync(ct);

    public async Task<BookingConfirmationData?> GetBookingConfirmationAsync(Guid orderId, CancellationToken ct)
    {
        var order = await context.PurchaseOrders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order is null) return null;
        var bookings = await Legs().Where(b => b.OrderId == orderId).ToListAsync(ct);
        var legs = bookings.OrderBy(b => b.Trip!.DepartureTime).Select(b => ToLeg(b, b.Tickets)).ToList();
        return new(order.Id, order.PurchaserName, order.PurchaserEmail, order.Status, order.PurchaseMode,
            legs.Sum(l => l.PaidAmount), order.Currency, legs);
    }

    public async Task<TripDisruptionData?> GetTripDisruptionAsync(Guid bookingId, CancellationToken ct)
    {
        var booking = await Legs().Include(b => b.Order).FirstOrDefaultAsync(b => b.Id == bookingId, ct);
        return booking?.Order is null ? null
            : new(booking.OrderId, booking.Order.PurchaserName, booking.Order.PurchaserEmail, booking.Order.Currency,
                ToLeg(booking, booking.Tickets));
    }

    public async Task<RefundSuccessData?> GetRefundSuccessAsync(Guid refundId, CancellationToken ct)
    {
        var refund = await context.FinancialTransactions.AsNoTracking()
            .Include(r => r.Order).Include(r => r.OriginalPayment)
            .Include(r => r.RefundAllocations).ThenInclude(a => a.Ticket).ThenInclude(t => t!.SeatReservation)
            .FirstOrDefaultAsync(r => r.Id == refundId && r.TransactionType == FinancialTransactionType.Refund, ct);
        if (refund?.Order is null || refund.OriginalPayment is null || refund.RefundBookingId is null) return null;
        var booking = await Legs().FirstOrDefaultAsync(b => b.Id == refund.RefundBookingId, ct);
        if (booking is null) return null;
        var allocations = refund.RefundAllocations.Where(a => a.Ticket?.SeatReservation != null)
            .Select(a => Line(a.Ticket!, a.Amount)).OrderBy(l => l.SeatCode).ToList();
        return new(refund.Id, refund.OrderId, refund.Order.PurchaserName, refund.Order.PurchaserEmail, refund.Status,
            refund.Amount, refund.OriginalPayment.Amount, refund.Currency, refund.GatewayReference, refund.CompletedAt,
            ToLeg(booking, []), allocations);
    }

    private IQueryable<Booking> Legs() => context.Bookings.AsNoTracking().AsSplitQuery()
        .Include(b => b.Trip).ThenInclude(t => t!.Route)
        .Include(b => b.BoardingCall).ThenInclude(c => c!.RouteStop).ThenInclude(s => s!.Station)
        .Include(b => b.DisembarkingCall).ThenInclude(c => c!.RouteStop).ThenInclude(s => s!.Station)
        .Include(b => b.Tickets).ThenInclude(t => t.SeatReservation);

    private static PassengerLine Line(Ticket t, decimal amount) => new(t.SeatReservation!.PassengerName,
        t.SeatReservation.SeatCodeAtSale ?? "", t.SeatReservation.SeatClassAtSale ?? "", t.TicketCode, amount);

    private static BookingLeg ToLeg(Booking b, IEnumerable<Ticket> tickets)
    {
        var passengers = tickets.Where(t => t.SeatReservation != null)
            .OrderBy(t => t.SeatReservation!.SeatCodeAtSale).ThenBy(t => t.TicketCode)
            .Select(t => Line(t, t.PaidFareSnapshot)).ToList();
        var trip = b.Trip!;
        // Cùng quy tắc với BookingQrService để QR trong email khớp credential hiện hành.
        var issuedAt = b.QrIssuedAt ?? b.ConfirmedAt ?? b.Tickets.Select(t => t.IssuedAt).Min() ?? b.CreatedAt;
        return new(b.Id, b.BookingCode, b.Status, b.PaidAllocation ?? b.QuotedTotal, b.PublicBookingId, b.QrCredentialVersion,
            issuedAt, trip.Id, trip.Status, trip.Route?.Name ?? "",
            b.BoardingCall?.RouteStop?.Station?.Name ?? "", b.DisembarkingCall?.RouteStop?.Station?.Name ?? "",
            b.BoardingCall?.EstimatedDepartureTime ?? trip.DepartureTime,
            b.DisembarkingCall?.EstimatedArrivalTime ?? trip.ArrivalTime, passengers);
    }
}
