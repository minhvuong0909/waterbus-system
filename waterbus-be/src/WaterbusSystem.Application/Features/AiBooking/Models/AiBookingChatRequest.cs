namespace WaterbusSystem.Application.Features.AiBooking.Models;

/// <summary>
/// Yêu cầu gửi từ chat widget của khách tới AI Booking Assistant.
/// </summary>
public record AiBookingChatRequest
{
    /// <summary>
    /// Câu hỏi hoặc yêu cầu bằng ngôn ngữ tự nhiên của khách.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Mã phiên hội thoại (tùy chọn) để hỗ trợ ngữ cảnh nhiều lượt (nếu có).
    /// </summary>
    public string? ConversationId { get; init; }

    /// <summary>
    /// Loại chuyến khách quan tâm ("Commuter", "Sightseeing" hoặc để trống), theo Domain.Enums.TripType.
    /// </summary>
    public string? PreferredServiceType { get; init; }
}
