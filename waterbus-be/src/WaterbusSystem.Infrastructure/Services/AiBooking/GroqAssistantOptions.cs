namespace WaterbusSystem.Infrastructure.Services.AiBooking;

/// <summary>
/// Cấu hình cho AI Booking Assistant sử dụng Groq API qua Semantic Kernel.
/// Bí mật (ApiKey) được cấu hình ngoài Git qua User Secrets hoặc biến môi trường.
/// </summary>
public class GroqAssistantOptions
{
    public const string SectionName = "AiBookingAssistant:Groq";
    public const string Endpoint = "https://api.groq.com/openai/v1";

    /// <summary>
    /// API key của Groq (bắt buộc cho môi trường chạy thật).
    /// Tuyệt đối không hard-code hoặc commit vào Git.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Model hỗ trợ tool calling trên Groq; có thể đổi qua cấu hình.
    /// </summary>
    public string ModelId { get; set; } = "openai/gpt-oss-20b";

    /// <summary>
    /// Giới hạn số lượt gọi tool tối đa trong một turn hội thoại nhằm ngăn ngừa vòng lặp vô hạn.
    /// </summary>
    public int MaxToolCallsPerTurn { get; set; } = 5;

    /// <summary>
    /// Thời gian chờ tối đa (giây) cho một lượt xử lý trước khi timeout. Mặc định 30 giây.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Cờ kích hoạt adapter production. Mặc định false cho đến khi có API key và contract backend ổn định.
    /// </summary>
    public bool IsEnabled { get; set; } = false;
}
