using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.ScannerDevices;
using WaterbusSystem.Application.DTOs.Responses.CheckIns;
using WaterbusSystem.Application.DTOs.Responses.ScannerDevices;
using WaterbusSystem.Application.Interfaces.Persistence;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Exceptions;

namespace WaterbusSystem.Application.Features.ScannerDevices.Services;

public sealed class StationScannerPolicy(
    IStationScannerRepository scanners, ICheckInRepository checkIns, IUnitOfWork unitOfWork,
    StationScannerAccessService access, IScannerAssignmentLock locks, IDateTimeService clock)
{
    public async Task<PaginatedList<ScannerAssignmentAdminResponse>> ListPrimaryAssignmentsAsync(
        Guid callId, int page, int size, CancellationToken ct)
    {
        await checkIns.GetRequiredStopCallAsync(callId, ct);
        return await scanners.ListPrimaryAssignmentsAsync(callId, clock.Now, page, size, ct);
    }

    public async Task<Guid> AssignPrimaryAsync(CreatePrimaryScannerRequest request, CancellationToken ct)
    {
        await using var deviceGate = await access.LockDeviceAsync(request.DeviceId, ct);
        await using var stopGate = await locks.TryAcquireAsync(request.TripStopCallId, StationScannerAccessService.LockWait, ct)
            ?? throw new ConflictException("Điểm dừng đang được xử lý. Vui lòng thử lại.");
        var now = clock.Now;
        var device = await checkIns.GetRequiredDeviceAsync(request.DeviceId, ct);
        if (!device.IsActive || device.StationId is not { } stationId ||
            !await scanners.IsBoardingCallAsync(request.TripStopCallId, stationId, historical: false, ct))
            throw new ConflictException("Chọn thiết bị đã ghép nối tại đúng bến lên khách.");
        if (request.ActiveFrom >= request.ActiveUntil || request.ActiveUntil <= now)
            throw new ValidationException([new("activeUntil", "Khoảng thời gian Primary Offline không hợp lệ.")]);

        var start = request.ActiveFrom > now ? request.ActiveFrom : now;
        if (await scanners.HasPrimaryConflictAsync(request.TripStopCallId, start, request.ActiveUntil, ct))
            throw new ConflictException("Điểm dừng còn Primary Offline hoặc quyền ngoại tuyến chưa hết hạn trong khoảng đã chọn.");

        var assignment = new ScannerAssignment { DeviceId = device.Id, TripStopCallId = request.TripStopCallId,
            IsPrimaryOffline = true, ActiveFrom = start.ToUniversalTime(), ActiveUntil = request.ActiveUntil.ToUniversalTime() };
        checkIns.AddAssignment(assignment);
        await unitOfWork.SaveChangesAsync(ct);
        return assignment.Id;
    }

    public async Task RevokePrimaryAsync(Guid id, CancellationToken ct)
    {
        var snapshot = await checkIns.GetRequiredAssignmentAsync(id, ct);
        await using var deviceGate = await access.LockDeviceAsync(snapshot.DeviceId, ct);
        await using var stopGate = await locks.TryAcquireAsync(snapshot.TripStopCallId, StationScannerAccessService.LockWait, ct)
            ?? throw new ConflictException("Điểm dừng đang được xử lý. Vui lòng thử lại.");
        if (snapshot.StaffAccountId is not null)
            throw new ConflictException("Phân công cũ chỉ dùng để tra cứu lịch sử.");
        var current = await checkIns.GetRequiredAssignmentAsync(id, ct);
        current.RevokedAt ??= clock.Now;
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<MyScannerAssignmentResponse>> ListCallsAsync(ScannerAccess scanner, DateTimeOffset now, CancellationToken ct)
    {
        var shift = await scanners.FindShiftAsync(scanner.StaffId, scanner.Scanner.StationId, now, ct);
        if (shift is null) return [];
        return await scanners.ListCallsAsync(scanner.Scanner, shift, now, ct);
    }

    public async Task<StaffStationShiftResponse?> GetShiftAsync(ScannerAccess scanner, CancellationToken ct)
    {
        var shift = await scanners.FindShiftAsync(scanner.StaffId, scanner.Scanner.StationId, clock.Now, ct);
        return shift is null ? null : new(shift.Id, shift.StaffAccountId, "",
            shift.StationId, scanner.Scanner.StationName, shift.ActiveFrom, shift.ActiveUntil, shift.RevokedAt);
    }

    public async Task<bool> CanOperateOnlineAsync(ScannerAccess scanner, Guid callId, DateTimeOffset now, CancellationToken ct) =>
        scanner.Enrollment.EndedAt is null &&
        await scanners.IsBoardingCallAsync(callId, scanner.Scanner.StationId, historical: false, ct) &&
        await scanners.FindShiftAsync(scanner.StaffId, scanner.Scanner.StationId, now, ct) is not null;

    public async Task<ScannerOfflineGrantResponse?> IssueOfflineGrantAsync(
        ScannerAccess scanner, TripStopCall call, DateTimeOffset now, CancellationToken ct)
    {
        var shift = await scanners.FindShiftAsync(scanner.StaffId, scanner.Scanner.StationId, now, ct);
        var primary = await scanners.FindPrimaryAsync(scanner.Scanner.DeviceId, call.Id, now, ct);
        if (shift is null || primary is null || primary.ActiveUntil is not { } primaryEnd ||
            call.CheckInOpenAt is not { } opens || call.CheckInCloseAt is not { } closes) return null;
        var start = new[] { now, primary.ActiveFrom, opens }.Max();
        var end = new[] { now.AddHours(4), shift.RevokedAt ?? shift.ActiveUntil, primaryEnd, closes }.Min();
        if (end <= start) return null;

        var grant = new ScannerOfflineGrant { EnrollmentId = scanner.Enrollment.Id, ScannerAssignmentId = primary.Id,
            StaffStationShiftId = shift.Id, ActiveFrom = start, ActiveUntil = end };
        scanners.AddGrant(grant);
        await unitOfWork.SaveChangesAsync(ct);
        return new(grant.Id, start, end);
    }

    public async Task<string?> OfflineDenialAsync(
        ScannerAccess scanner, Guid? grantId, Guid callId, DateTimeOffset occurredAt, CancellationToken ct)
    {
        if (grantId is not { } id) return "NotAssigned";
        var grant = await scanners.FindGrantAsync(id, ct);
        if (grant is null || grant.EnrollmentId != scanner.Enrollment.Id ||
            grant.StaffStationShift is not { } shift || shift.StaffAccountId != scanner.StaffId ||
            shift.StationId != scanner.Scanner.StationId ||
            grant.ScannerAssignment is not { } assignment || assignment.TripStopCallId != callId ||
            assignment.DeviceId != scanner.Scanner.DeviceId || occurredAt < grant.ActiveFrom || occurredAt >= grant.ActiveUntil ||
            !await scanners.IsBoardingCallAsync(callId, scanner.Scanner.StationId, historical: true, ct))
            return "NotAssigned";

        if (scanner.Enrollment.EndedAt is not null || !shift.Covers(occurredAt) ||
            (assignment.RevokedAt is { } cutoff && occurredAt >= cutoff))
            return "ReviewRequired";

        return null;
    }
}
