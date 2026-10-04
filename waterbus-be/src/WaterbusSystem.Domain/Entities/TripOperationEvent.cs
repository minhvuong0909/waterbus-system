using WaterbusSystem.Domain.Common;
namespace WaterbusSystem.Domain.Entities;
/// <summary>
/// Audit log mỗi lần TripStatus thay đổi.
/// Bắt buộc tạo 1 record này mỗi khi Trip.Status được cập nhật.
/// </summary>
public class TripOperationEvent : BaseEntity
{
    public Guid TripId { get; set; }
    public Trip? Trip { get; set; }
    public Guid? IncidentId { get; set; }
    public Incident? Incident { get; set; }
    public Guid? ActedByAdminId { get; set; }
    public Guid? ActedByCaptainId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
    public string? Note { get; set; }
}
