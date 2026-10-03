using WaterbusSystem.Domain.Common;

namespace WaterbusSystem.Domain.Entities;

/// <summary>
/// Theo dõi quá trình cập bến thực tế của từng chuyến đi (Arrival/Departure time, Staff/Captain ghi nhận).
/// </summary>
public class TripStopCall : BaseEntity
{
    public Guid TripId { get; set; }
    public Trip? Trip { get; set; }
    public Guid RouteStopId { get; set; }
    public RouteStop? RouteStop { get; set; }
    
    public int VisitOrder { get; set; }

    public DateTimeOffset? EstimatedArrivalTime { get; set; }
    public DateTimeOffset? EstimatedDepartureTime { get; set; }

    public DateTimeOffset? ActualArrivalTime { get; set; }
    public DateTimeOffset? ActualDepartureTime { get; set; }

    /// <summary>
    /// Pending, Arrived, Departed, Skipped
    /// </summary>
    public string Status { get; set; } = "Pending";
}
