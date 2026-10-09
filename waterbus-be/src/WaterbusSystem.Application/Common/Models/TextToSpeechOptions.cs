namespace WaterbusSystem.Application.Common.Models;

/// <summary>
/// Tùy chọn cho yêu cầu tổng hợp giọng nói (TTS).
/// Nếu để trống, hệ thống sẽ sử dụng cấu hình mặc định từ Infrastructure.
/// </summary>
public record TextToSpeechOptions
{
    /// <summary>
    /// Tên giọng đọc cụ thể (e.g. "vi-VN-HoaiMyNeural", "vi-VN-NamMinhNeural").
    /// </summary>
    public string? VoiceName { get; init; }

    /// <summary>
    /// Định dạng âm thanh đầu ra (e.g. "Audio24Khz48KBitRateMonoMp3", "Audio16Khz32KBitRateMonoMp3").
    /// </summary>
    public string? OutputFormat { get; init; }

    /// <summary>
    /// Thời gian chờ tối đa (timeout) cho lượt tổng hợp này.
    /// </summary>
    public TimeSpan? Timeout { get; init; }
}
