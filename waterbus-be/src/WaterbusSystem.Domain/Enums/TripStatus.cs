namespace WaterbusSystem.Domain.Enums;

/// <summary>
/// Trạng thái vận hành của chuyến tàu
/// </summary>
public enum TripStatus
{
    Scheduled = 1,  // Đã lên lịch
    Boarding = 2,   // Đang đón khách tại bến
    InTransit = 3,  // Tàu đang di chuyển trên sông
    Completed = 4,  // Chuyến đi hoàn thành
    Cancelled = 5,  // Hủy chuyến (do bão hoặc sự cố kỹ thuật)
    Delayed = 6     // Hoãn chuyến do thời tiết
}
