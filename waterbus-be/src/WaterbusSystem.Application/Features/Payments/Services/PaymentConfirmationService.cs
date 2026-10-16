using MediatR;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Bookings.Services;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;

using WaterbusSystem.Application.Features.Payments.Commands.ConfirmVnPayIpn;

namespace WaterbusSystem.Application.Features.Payments.Services;

public sealed class PaymentConfirmationService
{
    private readonly IApplicationDbContext _context;
    private readonly IVnPayService _gateway;
    private readonly ICommercialTransactionRunner _transactions;
    private readonly ICheckoutSessionService? _sessions;
    private readonly ISeatEventPublisher? _events;
    public PaymentConfirmationService(IApplicationDbContext context, IVnPayService vnPayService,
        ICommercialTransactionRunner transactions, ICheckoutSessionService? checkoutSessionService = null,
        ISeatEventPublisher? events = null)
    {
        _context = context; _gateway = vnPayService; _sessions = checkoutSessionService;
        _transactions = transactions; _events = events;
    }

    public async Task<VnPayIpnResultDto> ConfirmIpnAsync(ConfirmVnPayIpnCommand request, CancellationToken ct)
        => await ProcessAsync(request, true, ct);

    internal Task<VnPayIpnResultDto> ConfirmVerifiedAsync(ConfirmVnPayIpnCommand request, CancellationToken ct) => ProcessAsync(request, false, ct);

    private async Task<VnPayIpnResultDto> ProcessAsync(ConfirmVnPayIpnCommand request, bool checkSignature, CancellationToken ct)
    {
        if (checkSignature && !_gateway.ValidateSignature(request.Query)) return new("97", "Invalid Signature");
        if (checkSignature && !_gateway.IsValidTmnCode(request.Query.GetValueOrDefault("vnp_TmnCode", ""))) return new("99", "Invalid TmnCode");
        if (!long.TryParse(request.Query.GetValueOrDefault("vnp_Amount", ""), out var amount) || amount <= 0)
            return new("04", "Invalid Amount");
        var reference = request.Query.GetValueOrDefault("vnp_TxnRef", "");
        var transactionNo = request.Query.GetValueOrDefault("vnp_TransactionNo", "");
        if (string.IsNullOrEmpty(reference) || reference.Length > 100 || string.IsNullOrEmpty(transactionNo) || transactionNo.Length > 100)
            return new("99", "Input data required");
        Guid? changedOrderId = null;
        string eventStatus = "Released";
        var result = await _transactions.RunAsync(async () =>
        {
            var payment = await _context.FinancialTransactions.Include(p => p.Order)
                .FirstOrDefaultAsync(p => p.MerchantReference == reference && p.GatewayName == "VNPAY" &&
                    p.TransactionType == FinancialTransactionType.Payment, ct);
            if (payment?.Order is null) return new VnPayIpnResultDto("01", "Order not found");
            var order = payment.Order;
            if ((long)(payment.Amount * 100) != amount) return new VnPayIpnResultDto("04", "Invalid Amount");
            var key = reference + "_" + transactionNo;
            if (payment.Status == FinancialTransactionStatus.Succeeded ||
                await _context.FinancialTransactions.AnyAsync(p => p.IdempotencyKey == key, ct))
                return new VnPayIpnResultDto("02", "Order already confirmed");
            var bookings = await _context.Bookings.Include(b => b.Trip).Include(b => b.SeatReservations.Where(r => r.Status == ReservationStatus.Pending))
                .Where(b => b.OrderId == order.Id).ToListAsync(ct);
            if (bookings.Count == 0) return new VnPayIpnResultDto("01", "Order not found");
            var now = DateTimeOffset.UtcNow;
            var responseCode = request.Query.GetValueOrDefault("vnp_ResponseCode", "");
            var transactionStatus = request.Query.GetValueOrDefault("vnp_TransactionStatus", "");
            // Chưa hoàn tất/đang đảo: không kết luận thất bại, đợi querydr/IPN tiếp theo.
            if (transactionStatus is not ("00" or "02" or "07" or "09"))
                return new VnPayIpnResultDto("99", "Reconciliation required");
            var success = responseCode == "00" && transactionStatus == "00";
            payment.IdempotencyKey = key;
            payment.GatewayReference = transactionNo;
            payment.ProcessedAt = now;
            payment.Status = success ? FinancialTransactionStatus.Succeeded : FinancialTransactionStatus.Failed;
            payment.CompletedAt = success ? now : null;
            changedOrderId = order.Id;
            if (!success)
            {
                payment.FailureReason = "VNPAY payment failed: " + responseCode + "/" + transactionStatus;
                if (order.Status == OrderStatus.PendingPayment) Cancel(order, bookings);
            }
            else if (order.Status != OrderStatus.PendingPayment || order.ExpiresAt == null || order.ExpiresAt <= now ||
                bookings.Any(b => b.Status != BookingStatus.PendingPayment ||
                    b.SeatReservations.Count(r => r.Status == ReservationStatus.Pending) == 0) ||
                bookings.Any(b => b.Trip!.Status is not (TripStatus.Scheduled or TripStatus.Boarding) || b.Trip.ActualDepartureTime != null))
            {
                // Ghi nhận tiền thật nhưng KHÔNG hồi sinh ghế hết hạn: Admin phải đối soát/hoàn khoản này.
                payment.FailureReason = "ReconciliationRequired: payment received after checkout expired or trip became unavailable";
                if (order.Status == OrderStatus.PendingPayment) Cancel(order, bookings);
            }
            else
            {
                if (bookings.Sum(b => b.QuotedTotal) != order.QuotedTotal ||
                    bookings.Any(b => b.SeatReservations.Where(r => r.Status == ReservationStatus.Pending).Sum(r => r.QuotedFare) != b.QuotedTotal))
                    throw new InvalidOperationException("Payment allocation does not match Order.");
                order.Status = OrderStatus.Confirmed; order.ConfirmedAt = now; order.ExpiresAt = null;
                foreach (var booking in bookings)
                {
                    booking.Status = BookingStatus.Confirmed; booking.PaymentStatus = PaymentStatus.Success;
                    booking.ConfirmedAt = now; booking.QrIssuedAt = now; booking.PaidAllocation = booking.QuotedTotal;
                    foreach (var reservation in booking.SeatReservations.Where(r => r.Status == ReservationStatus.Pending).ToList())
                    {
                        reservation.Status = ReservationStatus.Confirmed;
                        reservation.PaidAllocation = reservation.QuotedFare; reservation.ConfirmedAt = now;
                        _context.Tickets.Add(Ticket.Issue(booking, reservation, payment, booking.Trip!.TripType, now));
                    }
                }
                eventStatus = "Sold";
            }
            await _context.SaveChangesAsync(ct);
            return new VnPayIpnResultDto("00", "Confirm Success");
        }, ct);
        if (changedOrderId.HasValue)
        {
            var reservations = await _context.SeatReservations.AsNoTracking()
                .Where(r => r.Booking!.OrderId == changedOrderId.Value &&
                    (eventStatus == "Sold" ? r.Status == ReservationStatus.Confirmed : r.Status == ReservationStatus.Cancelled)).ToListAsync(ct);
            await CheckoutSeatEvents.PublishAsync(_events, reservations, eventStatus, null, ct);
            if (_sessions is not null)
                try { await _sessions.ReleaseAsync("order:" + changedOrderId.Value, ct); } catch (Exception) { }
        }
        return result;
    }

    private static void Cancel(PurchaseOrder order, IEnumerable<Booking> bookings)
    {
        order.Status = OrderStatus.Expired; order.ExpiresAt = null;
        foreach (var booking in bookings.Where(b => b.Status == BookingStatus.PendingPayment))
        {
            booking.Status = BookingStatus.Cancelled; booking.PaymentStatus = PaymentStatus.Failed;
            foreach (var r in booking.SeatReservations.Where(r => r.Status == ReservationStatus.Pending)) r.Status = ReservationStatus.Cancelled;
        }
    }
}
