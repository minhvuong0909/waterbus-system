using WaterbusSystem.Application.Features.AiBooking.Models;

namespace WaterbusSystem.Application.Features.AiBooking.Interfaces;

/// <summary>
/// Tool tạo đường dẫn đặt vé cho chuyến tàu đã chọn.
/// Đảm bảo AI chỉ trả link do hệ thống sinh ra theo mẫu hợp lệ,
/// tuyệt đối không tự bịa URL hoặc tự ý tạo record Booking/Payment (tuân thủ BR-AI-01).
/// </summary>
public interface IBookingLinkTool
{
    Task<BookingLinkResult> BuildBookingLinkAsync(
        BookingLinkCriteria criteria,
        CancellationToken cancellationToken = default);
}
