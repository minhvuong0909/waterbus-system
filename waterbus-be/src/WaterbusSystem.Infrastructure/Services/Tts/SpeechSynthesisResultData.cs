namespace WaterbusSystem.Infrastructure.Services.Tts;

/// <summary>
/// Dữ liệu kết quả thô nhận được từ client SDK Azure Speech.
/// </summary>
public record SpeechSynthesisResultData(
    byte[] AudioData,
    TimeSpan Duration,
    bool Succeeded,
    string? CancellationReason = null,
    string? ErrorCode = null);
