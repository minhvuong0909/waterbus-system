namespace WaterbusSystem.Domain.Enums;

/// <summary>
/// Trạng thái đơn đặt chỗ (Booking)
/// </summary>
public enum BookingStatus
{
    Pending = 1,    // Đang giữ chỗ trong 10 phút, chờ thanh toán
    Confirmed = 2,  // Đã thanh toán thành công và xác nhận vé
    Cancelled = 3,  // Hết 10 phút không thanh toán hoặc bị hủy do thời tiết
    Refunded = 4    // Đã hoàn tiền
}
