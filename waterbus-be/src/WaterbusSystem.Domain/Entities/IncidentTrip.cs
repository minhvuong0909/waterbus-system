using WaterbusSystem.Domain.Common;
namespace WaterbusSystem.Domain.Entities;
/// <summary>
/// Liên kết giữa Incident và Trip bị ảnh hưởng.
/// OperationDecision là quyết định của Admin cho Trip này — KHÔNG tự động áp dụng.
/// </summary>
public class IncidentTrip : BaseEntity
{
    public Guid IncidentId { get; set; }
    public Incident? Incident { get; set; }
    public Guid TripId { get; set; }
    public Trip? Trip { get; set; }
    public Guid? DecisionByAdminId { get; set; }
    /// <summary>"Suspend", "Cancel", "None" — null nếu chưa quyết định</summary>
    public string? OperationDecision { get; set; }
    public DateTimeOffset? DecisionAt { get; set; }
}
