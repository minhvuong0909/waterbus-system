namespace WaterbusSystem.Infrastructure.Services.Tts;

/// <summary>
/// Cấu hình cho Azure AI Speech Service.
/// Các thông tin nhạy cảm (SubscriptionKey) lấy từ User Secrets hoặc biến môi trường,
/// tuyệt đối không hard-code trong mã nguồn hoặc commit vào Git.
/// </summary>
public class AzureSpeechOptions
{
    public const string SectionName = "AzureSpeech";

    /// <summary>
    /// Khóa dịch vụ Azure AI Speech (bắt buộc).
    /// </summary>
    public string? SubscriptionKey { get; set; }

    /// <summary>
    /// Vùng địa lý của tài nguyên Azure Speech (e.g. "southeastasia", "eastasia") (bắt buộc).
    /// </summary>
    public string? Region { get; set; }

    /// <summary>
    /// Giọng đọc tiếng Việt Neural được nhóm cấu hình cho môi trường.
    /// Không tự chọn giọng mặc định khi chưa được duyệt.
    /// </summary>
    public string? DefaultVoiceName { get; set; }

    /// <summary>
    /// Định dạng âm thanh đầu ra.
    /// Bắt buộc là một SpeechSynthesisOutputFormat dạng MP3.
    /// </summary>
    public string? OutputFormat { get; set; }

    /// <summary>
    /// Thời gian chờ tối đa (giây) cho một lượt tổng hợp TTS trước khi timeout.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}
