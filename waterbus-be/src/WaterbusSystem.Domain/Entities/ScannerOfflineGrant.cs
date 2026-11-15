using WaterbusSystem.Domain.Common;

namespace WaterbusSystem.Domain.Entities;

public sealed class ScannerOfflineGrant : BaseEntity
{
    public Guid EnrollmentId { get; init; }
    public ScannerEnrollment? Enrollment { get; init; }
    public Guid StaffStationShiftId { get; init; }
    public StaffStationShift? StaffStationShift { get; init; }
    public Guid ScannerAssignmentId { get; init; }
    public ScannerAssignment? ScannerAssignment { get; init; }
    public DateTimeOffset ActiveFrom { get; init; }
    public DateTimeOffset ActiveUntil { get; init; }
}
