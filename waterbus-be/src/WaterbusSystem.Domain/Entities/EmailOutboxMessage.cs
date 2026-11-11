using WaterbusSystem.Domain.Common;
using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Domain.Entities;

/// <summary>
/// Transactional outbox cho email giao dịch. Được INSERT trong CÙNG transaction với thay đổi nghiệp vụ;
/// worker gửi riêng sau khi commit. Chỉ lưu tham chiếu + người nhận (nội dung dựng từ dữ liệu đã commit lúc gửi).
/// <see cref="EventKey"/> là khoá nghiệp vụ tất định, UNIQUE ở DB ⇒ một sự kiện chỉ có một thông báo logic.
/// </summary>
public class EmailOutboxMessage : BaseEntity
{
    public string EventKey { get; set; } = string.Empty;
    public EmailNotificationType NotificationType { get; set; }
    public Guid OrderId { get; set; }
    public Guid? BookingId { get; set; }
    public Guid? FinancialTransactionId { get; set; }
    public string RecipientEmail { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
    public EmailOutboxStatus Status { get; set; } = EmailOutboxStatus.Pending;
    public int AttemptCount { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>Lease của worker; hết hạn ⇒ bản ghi Processing bị bỏ rơi và được claim lại.</summary>
    public DateTimeOffset? LockedUntil { get; set; }
    public Guid? LockToken { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public string? ProviderMessageId { get; set; }
    /// <summary>Chẩn đoán đã làm sạch (không chứa nội dung email/thông tin xác thực).</summary>
    public string? LastError { get; set; }
}
