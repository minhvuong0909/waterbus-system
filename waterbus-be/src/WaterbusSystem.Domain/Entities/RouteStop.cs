using WaterbusSystem.Domain.Common;

namespace WaterbusSystem.Domain.Entities;

/// <summary>
/// Danh sách bến dừng có thứ tự của một Tuyến đường.
/// SequenceNo: 1-indexed, theo chiều xuôi (chiều ngược dùng ScheduleStop.VisitOrder).
/// </summary>
public class RouteStop : BaseEntity
{
    public Guid RouteId { get; set; }
    public Route? Route { get; set; }
    public Guid StationId { get; set; }
    public Station? Station { get; set; }
    public int SequenceNo { get; set; }      // 1, 2, 3, ...
    public bool CanBoard { get; set; } = true;
    public bool CanAlight { get; set; } = true;

    public ICollection<ScheduleStop> ScheduleStops { get; set; } = new List<ScheduleStop>();
    public ICollection<TripStopCall> TripStopCalls { get; set; } = new List<TripStopCall>();
}
