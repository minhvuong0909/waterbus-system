namespace WaterbusSystem.Application.Features.AiBooking.Models;

/// <summary>
/// Kết quả sinh đường dẫn đặt vé do tool hệ thống tạo ra.
/// Đảm bảo link chỉ được tạo từ thông tin chuyến hợp lệ, không do AI tự bịa URL.
/// </summary>
/// <param name="BookingUrl">URL hợp lệ dẫn đến màn hình đặt chỗ (null nếu không hợp lệ).</param>
/// <param name="IsValid">Trạng thái tạo link thành công hay không.</param>
/// <param name="Message">Ghi chú hoặc lý do thất bại (nếu có).</param>
public record BookingLinkResult(
    string? BookingUrl,
    bool IsValid,
    string? Message = null);
