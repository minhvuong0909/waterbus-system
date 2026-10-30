using System.Text.Json.Serialization;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Application.Interfaces.Persistence;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Bookings.Services;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;

using WaterbusSystem.Application.DTOs.Responses.Payments;

namespace WaterbusSystem.Application.Features.Payments.Services;

public sealed class PaymentConfirmationService
{
    private readonly IFinancialTransactionRepository _payments;
    private readonly IBookingRepository _bookings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IVnPayService _gateway;
    private readonly ICommercialTransactionRunner _transactions;
    private readonly ICheckoutSessionService? _sessions;
    private readonly ISeatEventPublisher? _events;
    public PaymentConfirmationService(IFinancialTransactionRepository payments, IBookingRepository bookings, IUnitOfWork unitOfWork, IVnPayService vnPayService,
        ICommercialTransactionRunner transactions, ICheckoutSessionService? checkoutSessionService = null,
        ISeatEventPublisher? events = null)
    {
        _payments = payments; _bookings = bookings; _unitOfWork = unitOfWork; _gateway = vnPayService; _sessions = checkoutSessionService;
        _transactions = transactions; _events = events;
    }

    public async Task<VnPayIpnResultDto> ConfirmIpnAsync(IReadOnlyDictionary<string, string> query, CancellationToken ct)
        => await ProcessAsync(query, true, ct);

    internal Task<VnPayIpnResultDto> ConfirmVerifiedAsync(IReadOnlyDictionary<string, string> query, CancellationToken ct) => ProcessAsync(query, false, ct);

    private async Task<VnPayIpnResultDto> ProcessAsync(IReadOnlyDictionary<string, string> query, bool checkSignature, CancellationToken ct)
    {
        if (checkSignature && !_gateway.ValidateSignature(query)) return new("97", "Invalid Signature");
        if (checkSignature && !_gateway.IsValidTmnCode(query.GetValueOrDefault("vnp_TmnCode", ""))) return new("99", "Invalid TmnCode");
        if (!long.TryParse(query.GetValueOrDefault("vnp_Amount", ""), out var amount) || amount <= 0)
            return new("04", "Invalid Amount");
        var reference = query.GetValueOrDefault("vnp_TxnRef", "");
        var transactionNo = query.GetValueOrDefault("vnp_TransactionNo", "");
        if (string.IsNullOrEmpty(reference) || reference.Length > 100 || string.IsNullOrEmpty(transactionNo) || transactionNo.Length > 100)
            return new("99", "Input data required");
        Guid? changedOrderId = null;
        string eventStatus = "Released";
        var result = await _transactions.RunAsync(async () =>
        {
            var payment = await _payments.GetVnPayPaymentAsync(reference, ct);
            if (payment?.Order is null) return new VnPayIpnResultDto("01", "Order not found");
            var order = payment.Order;
            if ((long)(payment.Amount * 100) != amount) return new VnPayIpnResultDto("04", "Invalid Amount");
            var key = reference + "_" + transactionNo;
            var responseCode = query.GetValueOrDefault("vnp_ResponseCode", "");
            var transactionStatus = query.GetValueOrDefault("vnp_TransactionStatus", "");
            if (transactionStatus is not ("00" or "02" or "07" or "09"))
                return new VnPayIpnResultDto("99", "Reconciliation required");
            var success = responseCode == "00" && transactionStatus == "00";
            if (payment.Status == FinancialTransactionStatus.Succeeded)
            {
                if (success && (string.IsNullOrEmpty(payment.GatewayReference) || payment.GatewayReference == transactionNo))
                    return new VnPayIpnResultDto("02", "Order already confirmed");
                return await RecordConflictAsync(payment, responseCode, transactionStatus, transactionNo, ct);
            }
            if (payment.Status == FinancialTransactionStatus.Failed && !success && payment.IdempotencyKey == key)
                return new VnPayIpnResultDto("02", "Outcome already recorded");
            if (payment.IdempotencyKey != key && await _payments.HasIdempotencyKeyAsync(key, ct))
                return await RecordConflictAsync(payment, responseCode, transactionStatus, transactionNo, ct);
            var bookings = await _bookings.GetForPaymentConfirmationAsync(order.Id, ct);
            if (bookings.Count == 0) return new VnPayIpnResultDto("01", "Order not found");
            var now = DateTimeOffset.UtcNow;
            var wasFailed = payment.Status == FinancialTransactionStatus.Failed;
            var previousFailure = payment.FailureReason;
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
            else if (wasFailed || order.Status != OrderStatus.PendingPayment || order.ExpiresAt == null || order.ExpiresAt <= now ||
                bookings.Any(b => b.Status != BookingStatus.PendingPayment ||
                    b.SeatReservations.Count(r => r.Status == ReservationStatus.Pending) == 0) ||
                bookings.Any(b => b.Trip!.Status is not (TripStatus.Scheduled or TripStatus.Boarding) || b.Trip.ActualDepartureTime != null) ||
                await _bookings.HasUnavailableInventoryAsync(order.Id, ct))
            {
                payment.FailureReason = "ReconciliationRequired: payment received after checkout expired or trip became unavailable";
                if (!string.IsNullOrEmpty(previousFailure)) payment.FailureReason += "; previous outcome: " + previousFailure;
                if (order.Status == OrderStatus.PendingPayment) Cancel(order, bookings);
            }
            else
            {
                payment.FailureReason = null;
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
                        _bookings.AddTicket(Ticket.Issue(booking, reservation, payment, booking.Trip!.TripType, now));
                    }
                }
                eventStatus = "Sold";
            }
            await _unitOfWork.SaveChangesAsync(ct);
            return new VnPayIpnResultDto("00", "Confirm Success");
        }, ct);
        if (changedOrderId.HasValue)
        {
            var reservations = await _bookings.GetReservationsByStatusAsync(changedOrderId.Value,
                eventStatus == "Sold" ? ReservationStatus.Confirmed : ReservationStatus.Cancelled, ct);
            await CheckoutSeatEvents.PublishAsync(_events, reservations, eventStatus, null, ct);
            if (_sessions is not null)
                try { await _sessions.ReleaseAsync("order:" + changedOrderId.Value, ct); } catch (Exception) { }
        }
        return result;
    }

    private async Task<VnPayIpnResultDto> RecordConflictAsync(FinancialTransaction payment, string responseCode,
        string transactionStatus, string transactionNo, CancellationToken ct)
    {
        var evidence = $"VerifiedOutcomeConflict: {responseCode}/{transactionStatus}/{transactionNo}";
        if (payment.FailureReason?.Contains(evidence, StringComparison.Ordinal) != true)
        {
            payment.FailureReason = string.IsNullOrEmpty(payment.FailureReason)
                ? evidence : payment.FailureReason + "; " + evidence;
            await _unitOfWork.SaveChangesAsync(ct);
        }
        return new VnPayIpnResultDto("99", "Reconciliation required");
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
