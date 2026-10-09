namespace WaterbusSystem.Application.Features.AiBooking.Models;

/// <summary>
/// Phản hồi từ AI Booking Assistant trả về cho Frontend (chat widget).
/// Tách bạch rõ rệt giữa lời thoại tự nhiên (TextAnswer) và dữ liệu có cấu trúc (Cards).
/// </summary>
public record AiBookingChatResponse(
    string TextAnswer,
    IReadOnlyList<AiTripCardDto> Cards,
    IReadOnlyList<AiToolExecutionDto> ExecutedTools,
    bool IsFallback = false);
