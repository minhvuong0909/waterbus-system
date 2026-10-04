using WaterbusSystem.Domain.Common;
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
    public string TransactionType { get; set; } = "Payment";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    /// <summary>"Requested", "Processing", "Succeeded", "Failed"</summary>
    public string Status { get; set; } = "Requested";
    public string? GatewayName { get; set; }
    public string? GatewayReference { get; set; }
    /// <summary>Chống xử lý IPN trùng lặp — UNIQUE</summary>
    public string IdempotencyKey { get; set; } = string.Empty;
    public string? FailureReason { get; set; }
    public string? ManualRefundEvidence { get; set; }
    public Guid? ManualProcessedByAdminId { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public ICollection<RefundTicketAllocation> RefundAllocations { get; set; } = new List<RefundTicketAllocation>();
    public ICollection<FinancialTransaction> Refunds { get; set; } = new List<FinancialTransaction>();
}
