using WaterbusSystem.Domain.Common;
namespace WaterbusSystem.Domain.Entities;
/// <summary>
/// Phân công thiết bị quét cho nhân viên tại một TripStopCall cụ thể.
/// IsPrimaryOffline = true: thiết bị chính, được phép ghi offline (chỉ 1 per TripStopCall).
/// </summary>
public class ScannerAssignment : BaseEntity
{
    public Guid DeviceId { get; set; }
    public ScannerDevice? Device { get; set; }
    public Guid StaffAccountId { get; set; }
    public Guid TripStopCallId { get; set; }
    public TripStopCall? TripStopCall { get; set; }
    public bool IsPrimaryOffline { get; set; } = false;
    public DateTimeOffset ActiveFrom { get; set; }
    public DateTimeOffset? ActiveUntil { get; set; }
}
