using WaterbusSystem.Domain.Common;

namespace WaterbusSystem.Domain.Entities;

/// <summary>
/// Định nghĩa thứ tự và thời gian offset của từng bến trong một Schedule.
/// VisitOrder hỗ trợ chiều ngược (Inbound) và vòng lặp (loop routes).
/// </summary>
public class ScheduleStop : BaseEntity
{
    public Guid ScheduleId { get; set; }
    public Schedule? Schedule { get; set; }
    public Guid RouteStopId { get; set; }
    public RouteStop? RouteStop { get; set; }
    public int VisitOrder { get; set; }              // Thứ tự ghé trong chuyến
    public int? ArrivalOffsetMin { get; set; }       // Phút từ giờ khởi hành
    public int? DepartureOffsetMin { get; set; }
}
