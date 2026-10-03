using WaterbusSystem.Domain.Common;
using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Domain.Entities;

/// <summary>
/// Ghi nhận lịch sử gọi cổng thanh toán VNPAY / MoMo
/// </summary>
public class PaymentTransaction : BaseEntity
{
    public Guid BookingId { get; set; }
    public Booking? Booking { get; set; }

    /// <summary>
    /// Mã giao dịch gửi sang đối tác thanh toán
    /// </summary>
    public string TransactionCode { get; set; } = string.Empty;

    /// <summary>
    /// Tên nhà cung cấp cổng thanh toán (Ví dụ: VNPAY, MOMO)
    /// </summary>
    public string Provider { get; set; } = "VNPAY";

    /// <summary>
    /// Số tiền thanh toán (VNĐ)
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Thời điểm khách thanh toán trên cổng
    /// </summary>
    public DateTimeOffset? PayDate { get; set; }

    /// <summary>
    /// Mã phản hồi từ cổng (00: Thành công)
    /// </summary>
    public string? ResponseCode { get; set; }

    /// <summary>
    /// Trạng thái: Pending, Success, Failed
    /// </summary>
    public PaymentStatus TransactionStatus { get; set; } = PaymentStatus.Pending;

    /// <summary>
    /// Chữ ký số SecureHash nhận được từ Webhook IPN để đối soát
    /// </summary>
    public string? SecureHash { get; set; }
    
    /// <summary>
    /// Khóa chống xử lý trùng lặp IPN (Idempotency Key).
    /// Giá trị = vnp_TxnRef + "_" + vnp_TransactionNo
    /// </summary>
    public string IdempotencyKey { get; set; } = string.Empty;
}
