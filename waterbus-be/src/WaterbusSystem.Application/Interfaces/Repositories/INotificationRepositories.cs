using WaterbusSystem.Application.Features.Notifications;
using WaterbusSystem.Domain.Entities;

namespace WaterbusSystem.Application.Interfaces.Repositories;

public interface IEmailOutboxRepository
{
    /// <summary>Chỉ thêm vào DbContext hiện tại; caller commit cùng transaction nghiệp vụ.</summary>
    void Add(EmailOutboxMessage message);
    Task<bool> ExistsAsync(string eventKey, CancellationToken ct);

    /// <summary>
    /// Claim nguyên tử tối đa <paramref name="batchSize"/> bản ghi đến hạn (Pending/Failed hoặc Processing hết lease),
    /// đặt lease + <paramref name="lockToken"/> và tăng AttemptCount. Bản ghi Processing hết lease đã đạt
    /// <paramref name="maxAttempts"/> chuyển DeadLetter thay vì claim lại.
    /// </summary>
    Task<IReadOnlyList<EmailOutboxMessage>> ClaimAsync(DateTimeOffset now, int batchSize, TimeSpan lease,
        int maxAttempts, Guid lockToken, CancellationToken ct);

    // Mọi thao tác ghi kết quả có điều kiện LockToken: worker đã mất lease không ghi đè được. Trả false nếu mất lease.
    Task<bool> MarkSentAsync(Guid id, Guid lockToken, string? providerMessageId, DateTimeOffset now, CancellationToken ct);
    Task<bool> MarkRetryAsync(Guid id, Guid lockToken, DateTimeOffset nextAttemptAt, string error, CancellationToken ct);
    Task<bool> MarkDeadLetterAsync(Guid id, Guid lockToken, string error, CancellationToken ct);
    Task<bool> MarkSkippedAsync(Guid id, Guid lockToken, string reason, CancellationToken ct);
    /// <summary>Trả bản ghi về Pending (vd host đang dừng) và hoàn lại lượt thử.</summary>
    Task<bool> ReleaseAsync(Guid id, Guid lockToken, CancellationToken ct);
}

/// <summary>Read-model chỉ đọc cho việc chọn người nhận và dựng nội dung email từ dữ liệu đã commit.</summary>
public interface INotificationDataRepository
{
    Task<NotificationRecipient?> GetOrderRecipientAsync(Guid orderId, CancellationToken ct);
    /// <summary>Mỗi Booking Confirmed có Ticket của Trip, kèm người mua của Order.</summary>
    Task<IReadOnlyList<NotificationRecipient>> GetConfirmedBookingRecipientsForTripAsync(Guid tripId, CancellationToken ct);
    Task<NotificationRecipient?> GetRefundRecipientAsync(Guid refundId, CancellationToken ct);

    Task<BookingConfirmationData?> GetBookingConfirmationAsync(Guid orderId, CancellationToken ct);
    Task<TripDisruptionData?> GetTripDisruptionAsync(Guid bookingId, CancellationToken ct);
    Task<RefundSuccessData?> GetRefundSuccessAsync(Guid refundId, CancellationToken ct);
}
