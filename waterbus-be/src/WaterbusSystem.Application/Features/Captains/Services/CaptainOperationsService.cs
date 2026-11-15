using System.Text.Json;
using Microsoft.Extensions.Options;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.Captains;
using WaterbusSystem.Application.DTOs.Responses.Captains;
using WaterbusSystem.Application.Interfaces.Persistence;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Domain.Rules;

namespace WaterbusSystem.Application.Features.Captains.Services;

public sealed class CaptainOperationsService(ICaptainRepository repository, IUnitOfWork unitOfWork,
    ICommercialTransactionRunner transactions, ICurrentUserService currentUser, IDateTimeService clock,
    IOptions<CaptainOptions> options, ICaptainConnections connections) : ICaptainOperationsService
{
    private Guid Admin()
    {
        if (currentUser.UserRole != "Admin" || !Guid.TryParse(currentUser.UserId, out var id))
            throw new ForbiddenAccessException("Chỉ Admin được quản lý và điểm danh Captain.");
        return id;
    }
    private static string Reason(string reason) => !string.IsNullOrWhiteSpace(reason) && reason.Trim().Length <= 1000
        ? reason.Trim() : throw new ConflictException("Lý do bắt buộc, tối đa 1000 ký tự.");
    private DateTimeOffset RequiredAt(Trip trip)
    {
        var first = trip.TripStopCalls.Where(c => !c.IsDeleted && c.Status != "Skipped").OrderBy(c => c.VisitOrder).FirstOrDefault();
        return first?.CheckInOpenAt is { } opens ? CaptainRules.RequiredAt(opens, options.Value.PreparationMinutes)
            : throw new ConflictException("Chuyến chưa cấu hình giờ Staff mở check-in ở bến đầu.");
    }
    public async Task EnsureSellableAsync(Guid boatId, CancellationToken ct = default)
    {
        var boat = await repository.BoatAsync(boatId, ct);
        if (!boat.IsActive || boat.CaptainUserId is not { } captain || !await repository.IsEligibleAsync(captain, clock.Now, ct))
            throw new ConflictException("Tàu phải có Captain Active với giấy phép còn hạn trước khi mở bán/vận hành.");
    }
    public async Task EnsureStartAsync(Trip trip, CancellationToken ct = default)
    {
        if (trip.BoatId is not { } boatId) throw new ConflictException("Chuyến chưa được gán tàu.");
        await EnsureSellableAsync(boatId, ct);
        var boat = await repository.BoatAsync(boatId, ct);
        var attendance = await repository.AttendanceAsync(trip.Id, boatId, boat.CaptainAssignmentVersion, ct);
        if (attendance is null || attendance.CaptainId != boat.CaptainUserId)
            throw new ConflictException("Admin chưa ghi nhận Captain hiện tại có mặt cho chuyến này.");
    }
    public async Task ChangeAssignmentAsync(Boat boat, Guid? captainId, string reason, CancellationToken ct = default)
    {
        var admin = Admin();
        if (boat.CaptainUserId == captainId) return;
        reason = Reason(reason);
        var trips = await repository.BoatTripsAsync(boat.Id, ct);
        if (trips.Any(t => CaptainRules.BlocksAssignmentChange(t.Status)))
            throw new ConflictException("Không đổi/gỡ Captain khi tàu có chuyến Boarding, EnRoute hoặc Suspended.");
        if (captainId is { } id)
        {
            if (!await repository.IsEligibleAsync(id, clock.Now, ct)) throw new ConflictException("Captain không tồn tại, sai role, không Active hoặc giấy phép hết hạn.");
            var assigned = await repository.AssignedBoatAsync(id, ct);
            if (assigned is not null && assigned.Id != boat.Id) throw new ConflictException("Captain đã được gán cho tàu khác (1:1).");
        }
        var old = new { boat.CaptainUserId, boat.CaptainAssignmentVersion };
        var oldCaptain = boat.CaptainUserId;
        boat.CaptainUserId = captainId;
        boat.CaptainAssignmentVersion = Guid.NewGuid();
        // Thu hồi kết nối đang theo dõi tàu cũ; nếu transaction rollback, Captain có thể kết nối lại.
        if (oldCaptain.HasValue) connections.Revoke(oldCaptain.Value);
        repository.Add(new CaptainAuditEvent { CaptainId = captainId ?? oldCaptain!.Value, BoatId = boat.Id,
            ActedByAdminId = admin, OccurredAt = clock.Now, Action = "AssignmentChanged", Reason = reason,
            OldValues = JsonSerializer.Serialize(old), NewValues = JsonSerializer.Serialize(new { boat.CaptainUserId, boat.CaptainAssignmentVersion }) });
        if (oldCaptain is { } removed && captainId.HasValue)
            repository.Add(new CaptainAuditEvent { CaptainId = removed, BoatId = boat.Id,
                ActedByAdminId = admin, OccurredAt = clock.Now, Action = "AssignmentChanged", Reason = reason,
                OldValues = JsonSerializer.Serialize(old), NewValues = JsonSerializer.Serialize(new { boat.CaptainUserId, boat.CaptainAssignmentVersion }) });
        foreach (var trip in trips.Where(t => t.Status == TripStatus.Scheduled && (t.SalesCloseAt == null || t.SalesCloseAt > clock.Now)))
            repository.Add(new CaptainAlert { BoatId = boat.Id, TripId = trip.Id, CaptainId = captainId, OccurredAt = clock.Now,
                Type = "AssignmentChanged", DeduplicationKey = $"assignment:{trip.Id}:{boat.CaptainAssignmentVersion}",
                Message = "Captain của chuyến Scheduled đang mở bán đã thay đổi; Admin cần kiểm tra phân công và điểm danh." });
    }
    public Task AssignAsync(Guid boatId, AssignCaptainRequest request, CancellationToken ct = default) => transactions.RunAsync(async () =>
    {
        await ChangeAssignmentAsync(await repository.BoatAsync(boatId, ct), request.CaptainId, request.Reason, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return true;
    }, ct);
    public Task<CaptainAttendanceResponse> RecordAttendanceAsync(Guid tripId, RecordCaptainAttendanceRequest request, CancellationToken ct = default) => transactions.RunAsync(async () =>
    {
        var admin = Admin();
        var reason = Reason(request.Reason);
        var trip = await repository.TripAsync(tripId, ct);
        if (trip.Status is not (TripStatus.Scheduled or TripStatus.Boarding)) throw new ConflictException("Chỉ ghi nhận có mặt trước khi xuất bến.");
        var boat = trip.Boat ?? throw new ConflictException("Chuyến chưa có tàu.");
        if (boat.CaptainUserId != request.CaptainId || boat.CaptainAssignmentVersion != request.AssignmentVersion)
            throw new ConflictException("Phân công đã thay đổi; hãy tải lại Captain hiện tại.");
        await EnsureSellableAsync(boat.Id, ct);
        var existing = await repository.AttendanceAsync(trip.Id, boat.Id, boat.CaptainAssignmentVersion, ct);
        if (existing is not null) return Response(existing);
        var required = RequiredAt(trip);
        var entry = new CaptainAttendance { TripId = trip.Id, BoatId = boat.Id, CaptainId = request.CaptainId,
            AssignmentVersion = boat.CaptainAssignmentVersion, RecordedByAdminId = admin, Reason = reason,
            RequiredAt = required, RecordedAt = clock.Now, IsLate = CaptainRules.IsLate(clock.Now, required) };
        repository.Add(entry);
        repository.Add(new CaptainAuditEvent { CaptainId = entry.CaptainId, TripId = trip.Id, BoatId = boat.Id,
            ActedByAdminId = admin, OccurredAt = entry.RecordedAt, Action = "AttendanceRecorded", Reason = reason,
            NewValues = JsonSerializer.Serialize(entry) });
        await unitOfWork.SaveChangesAsync(ct);
        return Response(entry);
    }, ct);
    public async Task<CaptainTripAttendanceResponse> GetAttendanceAsync(Guid tripId, CancellationToken ct = default)
    {
        var trip = await repository.TripAsync(tripId, ct);
        if (currentUser.UserRole != "Admin" && !(currentUser.UserRole == "Captain" && Guid.TryParse(currentUser.UserId, out var id) && trip.Boat?.CaptainUserId == id))
            throw new ForbiddenAccessException("Chỉ xem điểm danh chuyến được phân công.");
        var boat = trip.Boat;
        var attendance = boat is null ? null : await repository.AttendanceAsync(trip.Id, boat.Id, boat.CaptainAssignmentVersion, ct);
        DateTimeOffset? required = trip.TripStopCalls.OrderBy(c => c.VisitOrder).FirstOrDefault(c => !c.IsDeleted && c.Status != "Skipped")?.CheckInOpenAt is { } opens
            ? CaptainRules.RequiredAt(opens, options.Value.PreparationMinutes) : null;
        return new(trip.Id, boat?.Id, boat?.CaptainUserId, boat?.CaptainAssignmentVersion, required,
            attendance is not null ? (attendance.IsLate ? "Late" : "Present") : required is null ? "NotConfigured" : clock.Now > required ? "Missing" : "Pending",
            attendance is null ? null : Response(attendance));
    }
    public Task<PaginatedList<CaptainAuditEvent>> AuditAsync(GetCaptainEventsRequest request, CancellationToken ct = default) { Admin(); return repository.AuditAsync(request, ct); }
    public Task<PaginatedList<CaptainAlert>> AlertsAsync(GetCaptainEventsRequest request, CancellationToken ct = default) { Admin(); return repository.AlertsAsync(request, ct); }
    public Task ResolveAlertAsync(Guid id, CancellationToken ct = default) => transactions.RunAsync(async () =>
    {
        var admin = Admin(); var alert = await repository.AlertAsync(id, ct);
        alert.ResolvedAt ??= clock.Now; alert.ResolvedByAdminId ??= admin;
        await unitOfWork.SaveChangesAsync(ct); return true;
    }, ct);
    public Task<int> CheckAttendanceAsync(CancellationToken ct = default) =>
        repository.CreateDueAttendanceAlertsAsync(clock.Now, options.Value.PreparationMinutes, options.Value.AlertBatchSize, ct);
    private static CaptainAttendanceResponse Response(CaptainAttendance a) => new(a.Id, a.TripId, a.BoatId, a.CaptainId,
        a.AssignmentVersion, a.RecordedByAdminId, a.RecordedAt, a.RequiredAt, a.IsLate, a.Reason);
}
