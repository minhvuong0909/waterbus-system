namespace WaterbusSystem.Application.Common.Exceptions;

/// <summary>
/// Ngoại lệ nảy sinh khi quá trình tổng hợp giọng nói (TTS) gặp sự cố
/// (lỗi provider, timeout, huỷ bỏ hoặc phản hồi không hợp lệ).
/// Đảm bảo không ghi đè hoặc để lộ API key/secret trong message.
/// </summary>
public class TextToSpeechException : Exception
{
    public string? ErrorCode { get; }

    public TextToSpeechException(string message)
        : base(message)
    {
    }

    public TextToSpeechException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public TextToSpeechException(string message, string? errorCode, Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}
