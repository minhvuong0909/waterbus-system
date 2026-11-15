namespace WaterbusSystem.Application.Common.Interfaces;

/// <summary>
/// Hàng đợi TRONG BỘ NHỚ gửi email xác minh tài khoản qua <see cref="IEmailTransport"/> (không qua EmailOutbox: outbox gắn với
/// sự kiện đơn hàng, không chứa được token). <see cref="Enqueue"/> không chặn và không ném lỗi, nên thời gian phản hồi API
/// không phụ thuộc việc email có tồn tại hay không.
/// </summary>
public interface IVerificationEmailQueue
{
    void Enqueue(string toEmail, string toName, string verificationLink);
}

/// <summary>Chặn gửi lặp (chống spam hộp thư người khác). Khoá hết hạn theo TTL.</summary>
public interface IEmailCooldownStore
{
    /// <summary>True nếu lấy được "chỗ gửi" (chưa có trong thời gian chờ). Redis không sẵn sàng ⇒ True (đã có rate limit theo IP).</summary>
    Task<bool> TryAcquireAsync(string key, TimeSpan ttl, CancellationToken ct);
}
