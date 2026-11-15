using WaterbusSystem.Domain.Common;

namespace WaterbusSystem.Domain.Entities;

/// <summary>Admin xác nhận hiện diện; giữ snapshot phân công và hạn để lịch sử không bị viết lại.</summary>
public sealed class CaptainAttendance : BaseEntity
{
    public Guid TripId { get; set; }
    public Guid BoatId { get; set; }
    public Guid CaptainId { get; set; }
    public Guid AssignmentVersion { get; set; }
    public Guid RecordedByAdminId { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
    public DateTimeOffset RequiredAt { get; set; }
    public bool IsLate { get; set; }
    public string Reason { get; set; } = "";
}
