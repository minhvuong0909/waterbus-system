using WaterbusSystem.Domain.Common;
namespace WaterbusSystem.Domain.Entities;
/// <summary>
/// Sự cố vận hành — kỹ thuật, an toàn, thời tiết, hoặc khác.
/// Phạm vi: Route, Boat, một hoặc nhiều Trip.
/// QUAN TRỌNG: Incident KHÔNG tự động thay đổi TripStatus — Admin quyết định qua IncidentTrip.
/// </summary>
public class Incident : BaseEntity
{
    /// <summary>"Route", "Boat", "Trip", "MultipleTrips"</summary>
    public string ScopeType { get; set; } = string.Empty;
    public Guid? RouteId { get; set; }
    public Route? Route { get; set; }
    public Guid? BoatId { get; set; }
    public Boat? Boat { get; set; }
    public Guid? ReportedByStaffId { get; set; }
    public Guid? ReportedByCaptainId { get; set; }
    public Guid? HandledByAdminId { get; set; }
    /// <summary>"Staff", "Captain", "System", "WeatherService"</summary>
    public string Source { get; set; } = string.Empty;
    /// <summary>"Technical", "Safety", "Weather", "Other"</summary>
    public string IncidentType { get; set; } = string.Empty;
    public string? Severity { get; set; }
    /// <summary>"Open", "Resolved", "Closed"</summary>
    public string Status { get; set; } = "Open";
    public string? Description { get; set; }
    public DateTimeOffset? OccurredAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public ICollection<IncidentTrip> AffectedTrips { get; set; } = new List<IncidentTrip>();
    public ICollection<TripOperationEvent> OperationEvents { get; set; } = new List<TripOperationEvent>();
}
