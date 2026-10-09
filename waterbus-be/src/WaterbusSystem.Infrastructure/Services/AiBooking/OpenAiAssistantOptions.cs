namespace WaterbusSystem.Infrastructure.Services.AiBooking;

/// <summary>
/// Cấu hình cho AI Booking Assistant sử dụng OpenAI API qua Semantic Kernel.
/// Bí mật (ApiKey) được cấu hình ngoài Git qua User Secrets hoặc biến môi trường.
/// </summary>
public class OpenAiAssistantOptions
{
    public const string SectionName = "AiBookingAssistant:OpenAI";

    /// <summary>
    /// API key của OpenAI (bắt buộc cho môi trường chạy thật).
    /// Tuyệt đối không hard-code hoặc commit vào Git.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Tên model khởi đầu theo quyết định tại AI_DECISIONS.md (mặc định "gpt-4.1-mini").
    /// </summary>
    public string ModelId { get; set; } = "gpt-4.1-mini";

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
