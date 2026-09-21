using WaterbusSystem.Domain.Common;
using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Domain.Entities;

/// <summary>
/// Khung lịch chạy cố định lặp lại theo tuần (Fixed Schedule Engine)
/// </summary>
public class Schedule : BaseEntity
{
    public Guid RouteId { get; set; }
    public Route? Route { get; set; }

    /// <summary>
    /// Giờ khởi hành cố định trong ngày (Ví dụ 07:30:00)
    /// </summary>
    public TimeSpan DepartureTime { get; set; }

    /// <summary>
    /// Các ngày trong tuần áp dụng (Chuỗi dạng "1,2,3,4,5,6,7" với 1 là Thứ 2)
    /// </summary>
    public string DaysOfWeek { get; set; } = "1,2,3,4,5,6,7";

    /// <summary>
    /// Loại chuyến: Thường (Commuter) hoặc Du lịch (Sightseeing)
    /// </summary>
    public TripType TripType { get; set; } = TripType.Commuter;

    public bool IsActive { get; set; } = true;
}
