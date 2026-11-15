using WaterbusSystem.Domain.Common;

namespace WaterbusSystem.Domain.Entities;

/// <summary>Cảnh báo vận hành bền vững cho Admin; không tự thay đổi Trip.Status.</summary>
public sealed class CaptainAlert : BaseEntity
{
    public string DeduplicationKey { get; set; } = "";
    public Guid? TripId { get; set; }
    public Guid BoatId { get; set; }
    public Guid? CaptainId { get; set; }
    public string Type { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public Guid? ResolvedByAdminId { get; set; }
}
