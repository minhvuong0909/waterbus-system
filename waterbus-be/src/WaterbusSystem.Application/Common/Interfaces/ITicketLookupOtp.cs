namespace WaterbusSystem.Application.Common.Interfaces;

/// <summary>Kết quả một lần thử xác minh OTP; <see cref="OtpHash"/> null khi không có OTP (hết hạn, đã dùng, hoặc vượt số lần thử).</summary>
public sealed record OtpAttempt(string? OtpHash, int Attempts);

/// <summary>
/// Lưu OTP tra cứu vé ở nơi có TTL (Redis). Chỉ nhận HASH của OTP, không bao giờ nhận OTP gốc. Mọi thao tác nguyên tử.
/// Ném <see cref="InvalidOperationException"/> khi kho không sẵn sàng (không được coi là "khớp").
/// </summary>
public interface ITicketLookupOtpStore
{
    /// <summary>Đặt chỗ cho một lần gửi OTP. False khi đang trong thời gian chờ hoặc vượt số lần gửi mỗi giờ.</summary>
    Task<bool> TryReserveSendAsync(string identity, TimeSpan cooldown, int maxPerHour, CancellationToken ct);

    /// <summary>Lưu hash OTP mới (ghi đè OTP cũ), đặt lại số lần thử về 0.</summary>
    Task SaveAsync(string identity, string otpHash, TimeSpan ttl, CancellationToken ct);

    /// <summary>
    /// Tăng số lần thử và trả hash đang lưu. Vượt <paramref name="maxAttempts"/> ⇒ huỷ OTP và trả OtpHash = null.
    /// </summary>
    Task<OtpAttempt> RegisterAttemptAsync(string identity, int maxAttempts, CancellationToken ct);

    /// <summary>Huỷ OTP. True chỉ cho một bên gọi duy nhất ⇒ OTP dùng một lần ngay cả khi có hai yêu cầu song song.</summary>
    Task<bool> RemoveAsync(string identity, CancellationToken ct);
}

/// <summary>Băm có khoá (HMAC) cho định danh tra cứu và OTP, để Redis không chứa email hay OTP rõ.</summary>
public interface ITicketLookupHasher
{
    string Hash(string purpose, string value);
    bool Equals(string hashA, string hashB);
}

/// <summary>
/// Gửi OTP qua email KHÔNG đi qua outbox (OTP ngắn hạn, không được lưu rõ trong DB, không được thử lại muộn).
/// <see cref="Enqueue"/> không chặn và không ném lỗi, nên thời gian phản hồi không phụ thuộc việc địa chỉ có tồn tại.
/// </summary>
public interface ITicketLookupOtpMailer
{
    void Enqueue(string toEmail, string toName, string otp, TimeSpan validFor);
}
