using WaterbusSystem.Domain.Common;

namespace WaterbusSystem.Domain.Entities;

public sealed class CaptainAuditEvent : BaseEntity
{
    public Guid CaptainId { get; set; }
    public Guid? BoatId { get; set; }
    public Guid? TripId { get; set; }
    public Guid ActedByAdminId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string Action { get; set; } = "";
    public string? OldValues { get; set; }
    public string NewValues { get; set; } = "";
    public string Reason { get; set; } = "";
}
