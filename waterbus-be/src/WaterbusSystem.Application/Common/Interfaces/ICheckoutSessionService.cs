namespace WaterbusSystem.Application.Common.Interfaces;

/// <summary>
/// Một ghế đang được giữ tạm cho một đoạn hành trình cụ thể.
/// Lưu VisitOrder chứ không lưu StationId vì đây là ảnh chụp của lần ghé thực tế trên Trip.
/// </summary>
public sealed record SeatHold(Guid TripId, Guid SeatId, int BoardingStopOrder, int DisembarkingStopOrder);

/// <summary>Trạng thái hiện tại của một phiên giữ ghế (CheckoutSession).</summary>
public sealed record CheckoutSessionState
{
    public required string SessionId { get; init; }
    public Guid OrderId { get; init; }
    public IReadOnlyList<SeatHold> Holds { get; init; } = [];
    /// <summary>Mốc hết hạn cố định, tính từ lần giữ hợp lệ ĐẦU TIÊN (BR-HOLD-02).</summary>
    public DateTimeOffset ExpiresAt { get; init; }

    /// <summary>BR-HOLD-03: sau khi gửi yêu cầu thanh toán có cửa sổ PaymentPending riêng.</summary>
    public bool IsPaymentPending { get; init; }

    public DateTimeOffset? PaymentPendingExpiresAt { get; init; }
}

/// <summary>
/// BR-HOLD-01: ghế được giữ theo CheckoutSession, KHÔNG khóa theo UserId, để Guest không cần
/// đăng nhập cũng dùng được.
/// BR-HOLD-03: bỏ chọn ghế sẽ giải phóng hold; client mất kết nối sẽ hết hạn qua TTL.
/// BR-HOLD-05: TTL do Admin/hệ thống cấu hình, không hard-code.
/// </summary>
public interface ICheckoutSessionService
{
    /// <summary>
    /// Giữ (hoặc bổ sung) ghế vào phiên. Hạn dùng chung cho toàn phiên và KHÔNG bị kéo dài khi
    /// thêm/bớt ghế, kể cả khi phiên phục vụ cả hai Booking của một Order round-trip.
    /// </summary>
    Task<CheckoutSessionState> HoldAsync(
        string sessionId, Guid orderId, IReadOnlyList<SeatHold> holds, CancellationToken cancellationToken = default);

    /// <summary>Đọc trạng thái phiên; trả null nếu không tồn tại hoặc đã hết hạn.</summary>
    Task<CheckoutSessionState> HoldUntilAsync(string sessionId, Guid orderId, IReadOnlyList<SeatHold> holds,
        DateTimeOffset expiresAt, CancellationToken cancellationToken = default);

    Task<CheckoutSessionState?> GetAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>Giải phóng toàn bộ hold của phiên. BR-HOLD-03 / BR-PAY-05.</summary>
    Task<bool> ReleaseAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// BR-HOLD-03: đánh dấu phiên đang chờ gateway xác nhận, có cửa sổ PaymentPending riêng
    /// để callback trễ vẫn còn cơ hội được đối soát.
    /// </summary>
    Task<CheckoutSessionState?> MarkPaymentPendingUntilAsync(string sessionId, Guid orderId,
        DateTimeOffset expiresAt, CancellationToken cancellationToken = default);

    Task<CheckoutSessionState?> MarkPaymentPendingAsync(
        string sessionId, Guid orderId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Kho lưu trữ TTL phía dưới Redis. Tách riêng để phần nghiệp vụ của phiên giữ ghế có thể
/// kiểm thử được mà không cần dựng Redis thật.
/// </summary>
public interface ICheckoutSessionStore
{
    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);
    Task SetAsync(string key, string value, TimeSpan ttl, CancellationToken cancellationToken = default);
    Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default);
}