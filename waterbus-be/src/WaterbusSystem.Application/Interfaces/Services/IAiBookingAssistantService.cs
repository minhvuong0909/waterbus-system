using WaterbusSystem.Application.DTOs.Responses.AiBooking;
using WaterbusSystem.Application.DTOs.Requests.AiBooking;
using WaterbusSystem.Application.Features.AiBooking.Models;

namespace WaterbusSystem.Application.Interfaces.Services;

/// <summary>
/// Dịch vụ AI Booking Assistant xử lý hội thoại người dùng,
/// điều phối tool calling (tìm chuyến, tạo link đặt vé) và trả lời có cấu trúc.
/// Application chỉ phụ thuộc interface này; Infrastructure cung cấp adapter
/// (Groq qua Semantic Kernel hoặc Fake cho test/offline).
/// </summary>
public interface IAiBookingAssistantService
{
    Task<AiBookingChatResponse> ProcessChatAsync(
        AiBookingChatRequest request,
        CancellationToken cancellationToken = default);
}
