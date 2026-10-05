using WaterbusSystem.Domain.Common;
using WaterbusSystem.Domain.Enums;
namespace WaterbusSystem.Domain.Entities;
/// <summary>
/// Giao dịch tài chính hợp nhất — xử lý cả Payment lẫn Refund.
/// Thay thế PaymentTransaction (sẽ deprecated ở migration sau).
/// </summary>
public class FinancialTransaction : BaseEntity
{
    public Guid OrderId { get; set; }
    public PurchaseOrder? Order { get; set; }
    /// <summary>Chỉ có giá trị khi TransactionType = "Refund"</summary>
    public Guid? RefundBookingId { get; set; }
    public Booking? RefundBooking { get; set; }
    /// <summary>FK tới Payment gốc — bắt buộc khi TransactionType = "Refund"</summary>
    public Guid? OriginalPaymentId { get; set; }
    public FinancialTransaction? OriginalPayment { get; set; }
    /// <summary>"Payment" hoặc "Refund"</summary>
    public FinancialTransactionType TransactionType { get; set; } = FinancialTransactionType.Payment;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    /// <summary>"Requested", "Processing", "Succeeded", "Failed"</summary>
    public FinancialTransactionStatus Status { get; set; } = FinancialTransactionStatus.Requested;
    public string? GatewayName { get; set; }
    public string? GatewayReference { get; set; }
    /// <summary>Merchant reference and creation time sent to the gateway, needed for query/refund.</summary>
    public string? MerchantReference { get; set; }
    public DateTimeOffset? GatewayRequestCreatedAt { get; set; }
    /// <summary>Chống xử lý IPN trùng lặp — UNIQUE</summary>
    public string IdempotencyKey { get; set; } = string.Empty;
    public string? FailureReason { get; set; }
    public string? ManualRefundEvidence { get; set; }
    public Guid? ManualProcessedByAdminId { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public ICollection<RefundTicketAllocation> RefundAllocations { get; set; } = new List<RefundTicketAllocation>();
    public ICollection<FinancialTransaction> Refunds { get; set; } = new List<FinancialTransaction>();

    /// <summary>Validate using the successful payment and current refundable balance loaded in a transaction.</summary>
    public void ValidateRefund(FinancialTransaction payment, Booking booking, decimal refundableBalance)
    {
        if (TransactionType != FinancialTransactionType.Refund ||
            payment.TransactionType != FinancialTransactionType.Payment ||
            payment.Status != FinancialTransactionStatus.Succeeded ||
            OriginalPaymentId != payment.Id || RefundBookingId != booking.Id ||
            OrderId != payment.OrderId || OrderId != booking.OrderId || Currency != payment.Currency ||
            booking.Status == BookingStatus.Completed)
            throw new InvalidOperationException("Refund must reference a successful Payment and Booking of the same Order.");
        if (Amount <= 0 || Amount > refundableBalance || Amount > payment.Amount ||
            Amount != RefundAllocations.Sum(a => a.Amount))
            throw new InvalidOperationException("Refund must equal its Ticket allocations and fit the refundable balance.");
        if (RefundAllocations.Select(a => a.TicketId).Distinct().Count() != RefundAllocations.Count ||
            RefundAllocations.Any(a => a.RefundTransactionId != Id || a.Ticket == null ||
                a.Ticket.Id != a.TicketId || a.Ticket.BookingId != booking.Id ||
                a.Ticket.BoardingStatus == BoardingStatus.NoShow || a.Ticket.Status == TicketStatus.Completed ||
                a.Amount <= 0 || a.Amount != a.Ticket.PaidFareSnapshot))
            throw new InvalidOperationException("Refund allocations must be distinct eligible Tickets of the affected Booking, at 100% of their paid fare.");
    }
}
