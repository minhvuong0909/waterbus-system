namespace WaterbusSystem.Domain.Enums;

/// <summary>
/// Trạng thái vận hành của chuyến tàu
/// </summary>
public enum TripStatus
{
    Scheduled  = 1,  // Đã lên lịch, chưa bắt đầu
    Boarding   = 2,  // Đang đón khách tại bến đầu
    EnRoute    = 3,  // Tàu đang di chuyển
    Suspended  = 4,  // Tạm dừng (sự cố, thời tiết)
    Arrived    = 5,  // Tàu đã cập bến cuối
    Completed  = 6,  // Chuyến hoàn thành (nghiệp vụ đóng)
    Cancelled  = 7,  // Hủy chuyến
    Terminated = 8   // Buộc dừng giữa chừng
}
