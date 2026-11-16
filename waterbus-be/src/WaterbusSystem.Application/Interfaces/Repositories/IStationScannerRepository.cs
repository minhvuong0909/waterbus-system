using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Responses.CheckIns;
using WaterbusSystem.Application.DTOs.Responses.ScannerDevices;
using WaterbusSystem.Domain.Entities;

namespace WaterbusSystem.Application.Interfaces.Repositories;

public interface IStationScannerRepository
{
    Task<Station> GetStationAsync(Guid id, CancellationToken ct);
    Task<ScannerEnrollment?> FindEnrollmentAsync(Guid id, CancellationToken ct);
    Task<ScannerEnrollment?> GetCurrentEnrollmentAsync(Guid deviceId, CancellationToken ct);
    void AddEnrollment(ScannerEnrollment enrollment);
    Task<StaffStationShift?> FindShiftAsync(Guid staffId, Guid stationId, DateTimeOffset at, CancellationToken ct);
    Task<bool> OverlapsShiftAsync(Guid staffId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct);
    Task<StaffStationShift> GetShiftAsync(Guid id, bool tracked, CancellationToken ct);
    void AddShift(StaffStationShift shift);
    Task<PaginatedList<StaffStationShiftResponse>> ListShiftsAsync(int page, int size, CancellationToken ct);
    Task<List<MyScannerAssignmentResponse>> ListCallsAsync(ScannerIdentityResponse scanner, StaffStationShift shift, DateTimeOffset now, CancellationToken ct);
    /// <summary>Cửa sổ check-in của điểm dừng đóng không muộn hơn lúc ca kết thúc (Staff chịu trách nhiệm đến hết cửa sổ).</summary>
    Task<bool> CallClosesWithinShiftAsync(Guid callId, DateTimeOffset shiftEnd, CancellationToken ct);
    Task<bool> IsBoardingCallAsync(Guid callId, Guid stationId, bool historical, CancellationToken ct);
    Task<PaginatedList<ScannerAssignmentAdminResponse>> ListPrimaryAssignmentsAsync(
        Guid callId, DateTimeOffset now, int page, int size, CancellationToken ct);
    Task<ScannerAssignment?> FindPrimaryAsync(Guid deviceId, Guid callId, DateTimeOffset now, CancellationToken ct);
    Task<bool> HasPrimaryConflictAsync(Guid callId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct);
    Task<List<ScannerAssignment>> ListCurrentPrimariesAsync(Guid deviceId, DateTimeOffset now, CancellationToken ct);
    void AddGrant(ScannerOfflineGrant grant);
    Task<ScannerOfflineGrant?> FindGrantAsync(Guid id, CancellationToken ct);
    Task<CheckInEvent> GetAuditEventAsync(Guid deviceId, Guid eventId, CancellationToken ct);
    Task<PaginatedList<ScannerAuditResponse>> ListAuditAsync(Guid deviceId, int page, int size, CancellationToken ct);
}
