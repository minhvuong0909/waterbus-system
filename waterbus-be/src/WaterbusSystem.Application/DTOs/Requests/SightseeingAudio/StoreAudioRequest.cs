namespace WaterbusSystem.Application.DTOs.Requests.SightseeingAudio;

/// <summary>
/// Dữ liệu yêu cầu lưu trữ file âm thanh MP3 từ Azure TTS vào kho lưu trữ bền vững.
/// </summary>
public record StoreAudioRequest
{
    /// <summary>
    /// Mảng byte dữ liệu âm thanh MP3.
    /// </summary>
    public byte[] AudioData { get; init; } = Array.Empty<byte>();

    /// <summary>
    /// Khóa do caller cung cấp, phải đổi khi nội dung/version kịch bản thay đổi
    /// (ví dụ: "pois/11111111-1111-1111-1111-111111111111/vi/v1.mp3").
    /// </summary>
    public string AssetKey { get; init; } = string.Empty;

    /// <summary>
    /// MIME type của âm thanh (mặc định "audio/mpeg").
    /// </summary>
    public string ContentType { get; init; } = "audio/mpeg";

    /// <summary>
    /// Mã POI liên kết (nếu có).
    /// </summary>
    public Guid? PoiId { get; init; }

    /// <summary>
    /// Mã ngôn ngữ (mặc định "vi" — theo scope hiện tại).
    /// </summary>
    public string LanguageCode { get; init; } = "vi";

    /// <summary>
    /// Thời lượng âm thanh (nếu có metadata từ TTS).
    /// </summary>
    public TimeSpan? Duration { get; init; }
}
