using WaterbusSystem.Application.Features.AiBooking.Models;

namespace WaterbusSystem.Application.DTOs.Responses.AiBooking;

/// <summary>
/// Phản hồi từ AI Booking Assistant trả về cho Frontend (chat widget).
/// Tách bạch rõ rệt giữa lời thoại tự nhiên (TextAnswer) và dữ liệu có cấu trúc (Cards).
/// </summary>
public record AiBookingChatResponse(
    string TextAnswer,
    IReadOnlyList<AiTripCardDto> Cards,
    IReadOnlyList<AiToolExecutionDto> ExecutedTools,
    bool IsFallback = false,
    TripSearchCriteria? SearchContext = null,
    AiSearchScopeClarification? PendingClarification = null);
