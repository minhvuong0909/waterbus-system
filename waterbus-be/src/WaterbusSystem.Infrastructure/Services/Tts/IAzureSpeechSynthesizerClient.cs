namespace WaterbusSystem.Infrastructure.Services.Tts;

/// <summary>
/// Client trừu tượng tương tác với Azure Speech SDK.
/// Tách biệt để cho phép unit test và mô phỏng lỗi/cancellation mà không cần credential hay kết nối mạng.
/// </summary>
public interface IAzureSpeechSynthesizerClient
{
    Task<SpeechSynthesisResultData> SynthesizeAsync(
        string text,
        string voiceName,
        string outputFormat,
        string subscriptionKey,
        string region,
        CancellationToken cancellationToken);
}
