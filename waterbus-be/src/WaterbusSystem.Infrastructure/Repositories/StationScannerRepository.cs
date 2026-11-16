using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Responses.CheckIns;
using WaterbusSystem.Application.DTOs.Responses.ScannerDevices;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure.Persistence;

namespace WaterbusSystem.Infrastructure.Repositories;

public sealed class StationScannerRepository(ApplicationDbContext db) : IStationScannerRepository
{
    public async Task<Station> GetStationAsync(Guid id, CancellationToken ct) =>
        await db.Stations.FirstOrDefaultAsync(s => s.Id == id && s.IsActive, ct)
        ?? throw new NotFoundException(nameof(Station), id);

    public Task<ScannerEnrollment?> FindEnrollmentAsync(Guid id, CancellationToken ct) =>
        db.ScannerEnrollments.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);

    public Task<ScannerEnrollment?> GetCurrentEnrollmentAsync(Guid deviceId, CancellationToken ct) =>
        db.ScannerEnrollments.FirstOrDefaultAsync(e => e.ScannerDeviceId == deviceId && e.EndedAt == null, ct);

    public void AddEnrollment(ScannerEnrollment enrollment) => db.ScannerEnrollments.Add(enrollment);

    public Task<StaffStationShift?> FindShiftAsync(Guid staffId, Guid stationId, DateTimeOffset at, CancellationToken ct) =>
        db.StaffStationShifts.AsNoTracking().FirstOrDefaultAsync(s => s.StaffAccountId == staffId && s.StationId == stationId
            && s.ActiveFrom <= at && at < s.ActiveUntil && (s.RevokedAt == null || at < s.RevokedAt), ct);

    public Task<bool> OverlapsShiftAsync(Guid staffId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct) =>
        db.StaffStationShifts.AsNoTracking().AnyAsync(s => s.StaffAccountId == staffId &&
            s.ActiveFrom < end && start < s.ActiveUntil &&
            (s.RevokedAt == null || (s.ActiveFrom < s.RevokedAt && start < s.RevokedAt)), ct);

    public async Task<StaffStationShift> GetShiftAsync(Guid id, bool tracked, CancellationToken ct) =>
        await (tracked ? db.StaffStationShifts : db.StaffStationShifts.AsNoTracking()).FirstOrDefaultAsync(s => s.Id == id, ct)
        ?? throw new NotFoundException(nameof(StaffStationShift), id);

    public void AddShift(StaffStationShift shift) => db.StaffStationShifts.Add(shift);

    public Task<PaginatedList<StaffStationShiftResponse>> ListShiftsAsync(int page, int size, CancellationToken ct) =>
        (from shift in db.StaffStationShifts.AsNoTracking()
         join staff in db.Users on shift.StaffAccountId equals staff.Id
         orderby shift.ActiveFrom descending, shift.Id
         select new StaffStationShiftResponse(shift.Id, staff.Id, staff.FullName, shift.StationId,
             shift.Station!.Name, shift.ActiveFrom, shift.ActiveUntil, shift.RevokedAt))
        .ToPaginatedListAsync(page, size, ct);

    private IQueryable<TripStopCall> BoardingCalls(Guid stationId) => db.TripStopCalls.AsNoTracking()
        .Where(c => c.RouteStop!.CanBoard && c.RouteStop.StationId == stationId);

    private static IQueryable<TripStopCall> Serviceable(IQueryable<TripStopCall> calls) =>
        calls.Where(c => c.Status != "Departed" && c.Status != "Skipped"
            && c.Trip!.Status != TripStatus.Cancelled && c.Trip.Status != TripStatus.Terminated
            && c.Trip.Status != TripStatus.Completed);

    private IQueryable<ScannerAssignment> Primaries => db.ScannerAssignments.Where(a => a.IsPrimaryOffline && a.StaffAccountId == null);

    public Task<List<MyScannerAssignmentResponse>> ListCallsAsync(ScannerIdentityResponse scanner, StaffStationShift shift,
        DateTimeOffset now, CancellationToken ct)
    {
        var shiftEnd = shift.EffectiveEnd;
        // Staff chỉ chịu trách nhiệm điểm dừng mà ca của họ kéo dài tới hết cửa sổ check-in (so với giờ ĐÓNG cửa sổ, không phải giờ mở):
        // ca kết thúc 15:30 thì không nhận chuyến có cửa sổ 15:15-15:45, vì 15:30-15:45 không còn ai trực.
        return Serviceable(BoardingCalls(scanner.StationId))
            .Where(c => c.CheckInCloseAt > now && c.CheckInCloseAt <= shiftEnd)
            .OrderBy(c => c.CheckInOpenAt).ThenBy(c => c.Id)
            .Select(c => new MyScannerAssignmentResponse(shift.Id, c.Id, c.TripId, c.Trip!.Route!.Name,
                scanner.StationName, c.VisitOrder, c.Trip.Status.ToString(), c.EstimatedArrivalTime,
                scanner.DeviceId, scanner.DeviceCode,
                Primaries.Any(a => a.TripStopCallId == c.Id && a.DeviceId == scanner.DeviceId
                    && a.RevokedAt == null && a.ActiveFrom < shiftEnd && a.ActiveUntil > now),
                shift.ActiveFrom, shiftEnd, c.CheckInOpenAt, c.CheckInCloseAt,
                c.Trip.DepartureTime, c.Trip.Boat != null ? c.Trip.Boat.Code : null,
                c.Trip.Boat != null ? c.Trip.Boat.Name : null, c.EstimatedDepartureTime))
            .ToListAsync(ct);
    }

    public Task<bool> CallClosesWithinShiftAsync(Guid callId, DateTimeOffset shiftEnd, CancellationToken ct) =>
        db.TripStopCalls.AsNoTracking().AnyAsync(c => c.Id == callId && c.CheckInCloseAt != null && c.CheckInCloseAt <= shiftEnd, ct);

    public Task<bool> IsBoardingCallAsync(Guid callId, Guid stationId, bool historical, CancellationToken ct)
    {
        var calls = BoardingCalls(stationId);
        return (historical ? calls : Serviceable(calls)).AnyAsync(c => c.Id == callId, ct);
    }

    public Task<PaginatedList<ScannerAssignmentAdminResponse>> ListPrimaryAssignmentsAsync(
        Guid callId, DateTimeOffset now, int page, int size, CancellationToken ct) =>
        Primaries.AsNoTracking().Where(a => a.TripStopCallId == callId && a.RevokedAt == null && a.ActiveUntil > now)
            .OrderBy(a => a.ActiveFrom).ThenBy(a => a.Id)
            .Select(a => new ScannerAssignmentAdminResponse(a.Id, Guid.Empty, null, a.Device!.DeviceCode,
                true, a.ActiveFrom, a.ActiveUntil))
            .ToPaginatedListAsync(page, size, ct);

    public Task<ScannerAssignment?> FindPrimaryAsync(Guid deviceId, Guid callId, DateTimeOffset now, CancellationToken ct) =>
        Primaries.AsNoTracking().Where(a => a.RevokedAt == null && a.DeviceId == deviceId
            && a.TripStopCallId == callId && a.ActiveUntil > now)
        .OrderBy(a => a.ActiveFrom).ThenBy(a => a.Id).FirstOrDefaultAsync(ct);

    public async Task<bool> HasPrimaryConflictAsync(Guid callId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct) =>
        await Primaries.AsNoTracking().AnyAsync(a => a.TripStopCallId == callId && a.RevokedAt == null &&
            a.ActiveFrom < end && a.ActiveUntil > start, ct)
        || await db.ScannerOfflineGrants.AsNoTracking().AnyAsync(g => g.ScannerAssignment!.TripStopCallId == callId
            && g.ActiveFrom < end && g.ActiveUntil > start, ct);

    public Task<List<ScannerAssignment>> ListCurrentPrimariesAsync(Guid deviceId, DateTimeOffset now, CancellationToken ct) =>
        Primaries.Where(a => a.DeviceId == deviceId && a.RevokedAt == null && a.ActiveUntil > now).ToListAsync(ct);

    public void AddGrant(ScannerOfflineGrant grant) => db.ScannerOfflineGrants.Add(grant);

    public Task<ScannerOfflineGrant?> FindGrantAsync(Guid id, CancellationToken ct) =>
        db.ScannerOfflineGrants.AsNoTracking().Include(g => g.StaffStationShift).Include(g => g.ScannerAssignment)
            .FirstOrDefaultAsync(g => g.Id == id, ct);

    public async Task<CheckInEvent> GetAuditEventAsync(Guid deviceId, Guid eventId, CancellationToken ct) =>
        await db.CheckInEvents.FirstOrDefaultAsync(e => e.Id == eventId && e.ScannerDeviceId == deviceId, ct)
        ?? throw new NotFoundException(nameof(CheckInEvent), eventId);

    public Task<PaginatedList<ScannerAuditResponse>> ListAuditAsync(Guid deviceId, int page, int size, CancellationToken ct) =>
        db.CheckInEvents.AsNoTracking().Where(e => e.ScannerDeviceId == deviceId)
            .OrderByDescending(e => e.ReceivedAtServer).ThenBy(e => e.Id)
            .Select(e => new ScannerAuditResponse(e.Id, e.TripStopCallId, e.StaffAccountId, e.TicketId,
                e.Outcome, e.OccurredAtDevice, e.ReceivedAtServer, e.ReviewedAt, e.ReviewNote)).ToPaginatedListAsync(page, size, ct);
}
