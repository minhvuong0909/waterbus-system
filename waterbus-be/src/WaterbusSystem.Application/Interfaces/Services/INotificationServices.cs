using WaterbusSystem.Application.Features.Notifications;
using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Application.Interfaces.Services;

/// <summary>
/// Ranh giới hẹp duy nhất mà service nghiệp vụ gọi để tạo thông báo. Chỉ thêm bản ghi outbox vào DbContext
/// hiện tại (KHÔNG SaveChanges, KHÔNG gọi provider): caller phải gọi trong transaction nghiệp vụ rồi commit.
/// Idempotent theo EventKey (pre-check + unique index DB).
/// </summary>
public interface INotificationOutbox
{
    Task EnqueueBookingConfirmedAsync(Guid orderId, CancellationToken ct);
    /// <param name="type">TripCancelled hoặc TripTerminated.</param>
    Task EnqueueTripDisruptionAsync(Guid tripId, EmailNotificationType type, CancellationToken ct);
    Task EnqueueRefundSucceededAsync(Guid refundId, CancellationToken ct);
}

public interface IEmailOutboxProcessor
{
    Task<EmailOutboxBatchResult> ProcessBatchAsync(CancellationToken ct);
}
