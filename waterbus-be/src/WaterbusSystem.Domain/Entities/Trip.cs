using WaterbusSystem.Domain.Common;
using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Domain.Entities;

/// <summary>
/// Chuyến tàu thực tế được sinh ra từ Lịch chạy cho từng ngày cụ thể
/// </summary>
public class Trip : BaseEntity
{
    public Guid? ScheduleId { get; set; }
    public Schedule? Schedule { get; set; }

    public Guid RouteId { get; set; }
    public Route? Route { get; set; }

    public Guid? BoatId { get; set; }
    public Boat? Boat { get; set; }

    /// <summary>
    /// Thời gian khởi hành thực tế của chuyến (có múi giờ)
    /// </summary>
    public DateTimeOffset DepartureTime { get; set; }

    /// <summary>
    /// Thời gian dự kiến cập bến
    /// </summary>
    public DateTimeOffset ArrivalTime { get; set; }

    /// <summary>
    /// Loại chuyến: Commuter hoặc Sightseeing
    /// </summary>
    public TripType TripType { get; set; } = TripType.Commuter;

    /// <summary>
    /// Trạng thái chuyến tàu: Scheduled, Boarding, EnRoute, Arrived, Completed, Cancelled, Suspended, Terminated
    /// </summary>
    public TripStatus Status { get; set; } = TripStatus.Scheduled;



    /// <summary>
    /// Danh sách các vé đã bán hoặc đang giữ cho chuyến tàu này
    /// </summary>
    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
    public ICollection<TripStopCall> TripStopCalls { get; set; } = new List<TripStopCall>();
}
