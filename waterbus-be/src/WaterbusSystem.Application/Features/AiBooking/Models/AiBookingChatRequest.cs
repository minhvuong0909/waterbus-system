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

    /// <summary>Các lượt gần nhất trước tin hiện tại; client gửi lại, server không lưu hội thoại.</summary>
    public IReadOnlyList<AiBookingChatTurn> History { get; init; } = Array.Empty<AiBookingChatTurn>();

    /// <summary>Tiêu chí từ lần tìm trước do client gửi lại; chỉ dùng khi khách yêu cầu sửa tìm kiếm cũ.</summary>
    public TripSearchCriteria? SearchContext { get; init; }

    /// <summary>Ngôn ngữ giao diện của khách: "vi" hoặc "en". Mặc định "vi" cho client cũ.</summary>
    public string? LanguageCode { get; init; }

    /// <summary>
    /// Mã phiên hội thoại (tùy chọn) để hỗ trợ ngữ cảnh nhiều lượt (nếu có).
    /// </summary>
    public string? ConversationId { get; init; }

    /// <summary>
    /// Loại chuyến khách quan tâm ("Commuter", "Sightseeing" hoặc để trống), theo Domain.Enums.TripType.
    /// </summary>
    public string? PreferredServiceType { get; init; }
}

public record AiBookingChatTurn(string Role, string Content);
