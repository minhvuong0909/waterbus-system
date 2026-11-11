using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.DTOs.Responses.CheckIns;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure.Persistence;

namespace WaterbusSystem.Infrastructure.Repositories;

public sealed class CheckInRepository(ApplicationDbContext context) : ICheckInRepository
{
    // ---------------- Scanner devices / assignments ----------------

    public async Task<ScannerDevice> GetRequiredDeviceAsync(Guid deviceId, CancellationToken cancellationToken = default)
    {
        var device = await context.ScannerDevices
            .FirstOrDefaultAsync(d => d.Id == deviceId && !d.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(ScannerDevice), deviceId);

        // Review PR #63: nếu context đã track thiết bị từ trước, EF trả lại instance cũ và KHÔNG ghi đè giá trị từ DB -
        // IsActive có thể đã bị Deactivate commit từ context khác. Làm tươi từ SQL (gọi sau khi giữ khoá DeviceId).
        await context.Entry(device).ReloadAsync(cancellationToken);
        // Reload của dòng bị xoá cứng (ngoài luồng hỗ trợ) làm EF detach instance nhưng giữ giá trị cũ: coi như không tồn tại.
        if (context.Entry(device).State == EntityState.Detached || device.IsDeleted) throw new NotFoundException(nameof(ScannerDevice), deviceId);
        return device;
    }

    public async Task<TripStopCall> GetRequiredStopCallAsync(Guid tripStopCallId, CancellationToken cancellationToken = default) =>
        await context.TripStopCalls
            .FirstOrDefaultAsync(c => c.Id == tripStopCallId && !c.IsDeleted, cancellationToken)
        ?? throw new NotFoundException(nameof(TripStopCall), tripStopCallId);

    public Task<bool> HasOverlappingPrimaryAssignmentAsync(
        Guid tripStopCallId, DateTimeOffset activeFrom, DateTimeOffset requestedEnd, CancellationToken cancellationToken = default) =>
        context.ScannerAssignments.AsNoTracking().AnyAsync(a =>
                !a.IsDeleted &&
                a.TripStopCallId == tripStopCallId &&
                a.IsPrimaryOffline &&
                a.ActiveFrom < requestedEnd &&
                (a.ActiveUntil ?? DateTimeOffset.MaxValue) > activeFrom,
            cancellationToken);

    public void AddAssignment(ScannerAssignment assignment) => context.ScannerAssignments.Add(assignment);

    public async Task<ScannerAssignment> GetRequiredAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken = default) =>
        await context.ScannerAssignments
            .FirstOrDefaultAsync(a => a.Id == assignmentId && !a.IsDeleted, cancellationToken)
        ?? throw new NotFoundException(nameof(ScannerAssignment), assignmentId);

    public Task<bool> HasActiveAssignmentAsync(Guid staffId, Guid tripStopCallId, DateTimeOffset now, CancellationToken cancellationToken = default) =>
        context.ScannerAssignments.AsNoTracking().AnyAsync(a =>
                a.StaffAccountId == staffId &&
                a.TripStopCallId == tripStopCallId &&
                a.ActiveFrom <= now &&
                (a.ActiveUntil == null || a.ActiveUntil >= now),
            cancellationToken);

    public Task<List<MyScannerAssignmentResponse>> ListMyAssignmentsAsync(
        Guid staffId, DateTimeOffset now, CancellationToken cancellationToken = default) =>
        context.ScannerAssignments.AsNoTracking()
            .Where(a => !a.IsDeleted && a.StaffAccountId == staffId &&
                        (a.ActiveUntil == null || a.ActiveUntil >= now))
            .OrderBy(a => a.ActiveFrom)
            .Select(a => new MyScannerAssignmentResponse(
                a.Id,
                a.TripStopCallId,
                a.TripStopCall!.TripId,
                a.TripStopCall.Trip!.Route!.Name,
                a.TripStopCall.RouteStop!.Station!.Name,
                a.TripStopCall.VisitOrder,
                a.TripStopCall.Trip.Status.ToString(),
                a.TripStopCall.EstimatedArrivalTime,
                a.DeviceId,
                a.Device!.DeviceCode,
                a.IsPrimaryOffline,
                a.ActiveFrom,
                a.ActiveUntil,
                a.TripStopCall.CheckInOpenAt,
                a.TripStopCall.CheckInCloseAt))
            .ToListAsync(cancellationToken);

    // ---------------- Check-in events (idempotency) ----------------

    public Task<CheckInEvent?> FindEventByClientIdAsync(string clientEventId, CancellationToken cancellationToken = default) =>
        context.CheckInEvents.FirstOrDefaultAsync(e => e.ClientEventId == clientEventId, cancellationToken);

    public Task<Ticket?> GetTicketWithReservationAsync(Guid ticketId, CancellationToken cancellationToken = default) =>
        context.Tickets.AsNoTracking()
            .Include(t => t.SeatReservation)
            .FirstOrDefaultAsync(t => t.Id == ticketId, cancellationToken);

    public async Task<Ticket> GetRequiredTicketWithBookingAsync(Guid ticketId, CancellationToken cancellationToken = default) =>
        await context.Tickets
            .Include(t => t.SeatReservation!).ThenInclude(r => r!.Booking)
            .FirstOrDefaultAsync(t => t.Id == ticketId && !t.IsDeleted, cancellationToken)
        ?? throw new NotFoundException(nameof(Ticket), ticketId);

    public void AddCheckInEvent(CheckInEvent checkInEvent) => context.CheckInEvents.Add(checkInEvent);

    // ---------------- Manifest ----------------

    public async Task<TripStopCall> GetStopCallForManifestAsync(Guid tripStopCallId, CancellationToken cancellationToken = default) =>
        await context.TripStopCalls.AsNoTracking()
            .Include(c => c.RouteStop!).ThenInclude(rs => rs!.Station)
            .Include(c => c.Trip!).ThenInclude(t => t!.Route!).ThenInclude(r => r!.DepartureStation)
            .Include(c => c.Trip!).ThenInclude(t => t!.Route!).ThenInclude(r => r!.ArrivalStation)
            .Include(c => c.Trip!).ThenInclude(t => t!.Boat)
            .FirstOrDefaultAsync(c => c.Id == tripStopCallId && !c.IsDeleted, cancellationToken)
        ?? throw new NotFoundException(nameof(TripStopCall), tripStopCallId);

    public Task<List<Ticket>> ListTicketsForManifestAsync(Guid tripStopCallId, CancellationToken cancellationToken = default) =>
        context.Tickets.AsNoTracking()
            .Include(t => t.SeatReservation!).ThenInclude(r => r!.Booking)
            .Where(t => !t.IsDeleted && t.Status == TicketStatus.Valid &&
                        t.SeatReservation!.Booking!.BoardingCallId == tripStopCallId)
            .ToListAsync(cancellationToken);

    // ---------------- NoShow finalization ----------------

    public Task<List<Guid>> ListClosedStopCallIdsAsync(DateTimeOffset deadline, CancellationToken cancellationToken = default) =>
        context.TripStopCalls.AsNoTracking()
            .Where(c => !c.IsDeleted && c.CheckInCloseAt != null && c.CheckInCloseAt <= deadline
                        && c.Trip!.Status != TripStatus.Cancelled && c.Trip!.Status != TripStatus.Terminated)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

    public Task<List<Guid>> ListUnresolvedStopCallIdsAsync(IReadOnlyCollection<Guid> stopCallIds, CancellationToken cancellationToken = default) =>
        context.CheckInEvents.AsNoTracking()
            .Where(e => stopCallIds.Contains(e.TripStopCallId) &&
                        (e.SyncStatus == "Pending" || e.SyncStatus == "Conflict"))
            .Select(e => e.TripStopCallId)
            .Distinct()
            .ToListAsync(cancellationToken);

    public Task<List<Ticket>> ListNoShowCandidateTicketsAsync(IReadOnlyCollection<Guid> eligibleStopCallIds, CancellationToken cancellationToken = default) =>
        context.Tickets
            .Where(t => !t.IsDeleted &&
                        t.Status == TicketStatus.Valid &&
                        t.BoardingStatus == BoardingStatus.NotCheckedIn &&
                        eligibleStopCallIds.Contains(t.SeatReservation!.Booking!.BoardingCallId))
            .ToListAsync(cancellationToken);
}
