using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.ScannerDevices;
using WaterbusSystem.Application.DTOs.Responses.ScannerDevices;
using WaterbusSystem.Application.Interfaces.Persistence;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;
using ValidationException = WaterbusSystem.Application.Common.Exceptions.ValidationException;

namespace WaterbusSystem.Application.Features.ScannerDevices.Services;

public sealed class StationScannerService(
    IStationScannerRepository scanners, IScannerDeviceAdminRepository devices,
    StationScannerAccessService access, IScannerAssignmentLock locks,
    IScannerSecrets secrets, IDateTimeService clock, IUnitOfWork unitOfWork,
    ICheckInRepository checkIns, ICurrentUserService user) : IStationScannerService
{
    private const string ActivationPrefix = "waterbus-pair:v1:";

    public async Task<Guid> CreateAsync(CreateStationScannerRequest request, CancellationToken ct)
    {
        var station = await scanners.GetStationAsync(request.StationId, ct);
        await using var gate = await locks.TryAcquireAsync(station.Id, StationScannerAccessService.LockWait, ct)
            ?? throw new ConflictException("Đang tạo thiết bị tại bến này. Vui lòng thử lại.");
        var stationCode = new string(station.Code.Where(char.IsAsciiLetterOrDigit).Take(24).ToArray()).ToUpperInvariant();
        if (stationCode.Length == 0) stationCode = station.Id.ToString("N")[..12].ToUpperInvariant();

        var number = 1;
        string code;
        do
        {
            code = $"SC-{stationCode}-{number++:D3}";
        } while (await devices.ExistsByCodeAsync(code, ct));
        var scanner = ScannerDevice.Register(station, code, request.Label?.Trim());
        devices.Add(scanner);
        await unitOfWork.SaveChangesAsync(ct);
        return scanner.Id;
    }

    public async Task<ScannerInvitationResponse> InviteAsync(Guid id, CancellationToken ct)
    {
        await using var gate = await access.LockDeviceAsync(id, ct);
        var scanner = await devices.GetTrackedAsync(id, ct);
        if (scanner.StationId is not { } stationId || scanner.IsActive || scanner.RevokedAt != null || scanner.SuspendedAt != null)
            throw new ConflictException("Thiết bị phải được gắn bến và ở trạng thái chờ kích hoạt.");
        await scanners.GetStationAsync(stationId, ct);

        var secret = secrets.Generate();
        var expires = clock.Now.AddMinutes(10);
        scanner.Invite(secrets.Hash(secret), expires);
        await unitOfWork.SaveChangesAsync(ct);
        return new(ActivationPrefix + $"{id:N}.{secret}", expires);
    }

    public async Task CancelInvitationAsync(Guid id, CancellationToken ct)
    {
        await using var gate = await access.LockDeviceAsync(id, ct);
        var scanner = await devices.GetTrackedAsync(id, ct);
        scanner.ClearInvitation();
        await unitOfWork.SaveChangesAsync(ct);
    }

    public Task SuspendAsync(Guid id, CancellationToken ct) => ChangeStateAsync(id, "Suspended", null, ct);
    public Task RevokeAsync(Guid id, CancellationToken ct) => ChangeStateAsync(id, "Revoked", null, ct);
    public Task ResetAsync(Guid id, CancellationToken ct) => ChangeStateAsync(id, "PendingActivation", null, ct);

    public Task TransferAsync(Guid id, TransferScannerRequest request, CancellationToken ct) =>
        ChangeStateAsync(id, "PendingActivation", request.StationId, ct);

    private async Task ChangeStateAsync(Guid id, string status, Guid? stationId, CancellationToken ct)
    {
        await using var gate = await access.LockDeviceAsync(id, ct);
        var scanner = await devices.GetTrackedAsync(id, ct);
        var station = stationId is { } targetId ? await scanners.GetStationAsync(targetId, ct) : null;
        if (station is null && scanner.StationId is null && status == "PendingActivation")
            throw new ConflictException("Chọn bến cho thiết bị cũ trước khi ghép nối.");
        if (scanner.RevokedAt != null && status == "Suspended")
            throw new ConflictException("Thiết bị đã bị thu hồi.");

        var now = clock.Now;
        var enrollment = await scanners.GetCurrentEnrollmentAsync(id, ct);
        if (enrollment is not null) enrollment.EndedAt = now;
        foreach (var assignment in await scanners.ListCurrentPrimariesAsync(id, now, ct))
            assignment.RevokedAt = now;

        scanner.IsActive = false;
        scanner.ClearInvitation();
        scanner.RevokedAt = status == "Revoked" ? scanner.RevokedAt ?? now : null;
        scanner.SuspendedAt = status == "Suspended" ? scanner.SuspendedAt ?? now : null;
        if (station is not null) scanner.TransferTo(station);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<ScannerPairingResponse> PairAsync(RedeemScannerInvitationRequest request, CancellationToken ct)
    {
        await access.RequireStaffAsync(ct);
        if (!request.Activation.StartsWith(ActivationPrefix, StringComparison.Ordinal)
            || !StationScannerAccessService.TryParseSecret(request.Activation[ActivationPrefix.Length..], out var id, out var secret))
            throw InvalidInvitation();

        await using var gate = await access.LockDeviceAsync(id, ct);
        ScannerDevice scanner;
        try { scanner = await devices.GetTrackedAsync(id, ct); }
        catch (NotFoundException) { throw InvalidInvitation(); }

        var now = clock.Now;
        if (scanner.StationId is not { } stationId || scanner.IsActive || scanner.RevokedAt != null || scanner.SuspendedAt != null
            || scanner.InvitationExpiresAt is not { } expires || expires <= now
            || !secrets.Matches(secret, scanner.InvitationHash))
            throw InvalidInvitation();

        var station = await scanners.GetStationAsync(stationId, ct);
        var credential = secrets.Generate();
        var enrollment = new ScannerEnrollment
        {
            ScannerDeviceId = id, StationId = station.Id, PairedAt = now, CredentialHash = secrets.Hash(credential)
        };
        scanners.AddEnrollment(enrollment);
        scanner.ClearInvitation();
        scanner.IsActive = true;
        await unitOfWork.SaveChangesAsync(ct);
        return new(new(id, enrollment.Id, scanner.DeviceCode, station.Id, station.Name), $"{enrollment.Id:N}.{credential}");
    }

    public async Task ResolveReviewAsync(Guid deviceId, Guid eventId, ResolveScannerReviewRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(user.UserId, out var reviewerId) || user.UserRole != "Admin")
            throw new WaterbusSystem.Application.Common.Exceptions.ForbiddenAccessException("Chỉ quản trị viên được đối soát.");
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 500)
            throw new ValidationException([new("reason", "Nhập lý do đối soát (tối đa 500 ký tự).")]);

        await using var gate = await access.LockDeviceAsync(deviceId, ct);
        var evt = await scanners.GetAuditEventAsync(deviceId, eventId, ct);
        if (evt.Outcome != "ReviewRequired" || evt.ScannerOfflineGrantId is not { } grantId)
            throw new ConflictException("Sự kiện không ở trạng thái chờ đối soát.");

        if (request.Approve)
        {
            var grant = await scanners.FindGrantAsync(grantId, ct);
            var enrollment = evt.ScannerEnrollmentId is { } id ? await scanners.FindEnrollmentAsync(id, ct) : null;
            var at = evt.OccurredAtDevice;
            if (grant?.StaffStationShift?.Covers(at) != true || enrollment is null ||
                (enrollment.EndedAt is { } ended && at >= ended) ||
                (grant.ScannerAssignment?.RevokedAt is { } revoked && at >= revoked))
                throw new ConflictException("Thời điểm soát vé nằm ngoài quyền tác nghiệp đã cấp. Hãy từ chối sự kiện.");

            var ticket = await checkIns.GetRequiredTicketWithBookingAsync(evt.TicketId, ct);
            var call = await checkIns.GetStopCallForManifestAsync(evt.TripStopCallId, ct);
            var booking = ticket.SeatReservation?.Booking;
            if (ticket.Status != TicketStatus.Valid || booking?.BoardingCallId != call.Id || booking.TripId != call.TripId ||
                call.Trip?.Status is TripStatus.Cancelled or TripStatus.Terminated)
                throw new ConflictException("Vé hoặc chuyến đã thay đổi. Hãy từ chối sự kiện.");

            if (ticket.BoardingStatus != BoardingStatus.CheckedIn)
            {
                ticket.BoardingStatus = BoardingStatus.CheckedIn;
                ticket.CheckedInAt = at;
                ticket.CheckedInByStaffId = evt.StaffAccountId;
                ticket.NoShowFinalizedAt = null;
                evt.Outcome = "Success";
            }
            else evt.Outcome = "AlreadyCheckedIn";
        }
        else evt.Outcome = "ReviewRejected";

        evt.SyncStatus = "Synced";
        evt.ReviewedAt = clock.Now;
        evt.ReviewedByAccountId = reviewerId;
        evt.ReviewNote = request.Reason.Trim();
        await unitOfWork.SaveChangesAsync(ct);
    }

    public Task<PaginatedList<ScannerAuditResponse>> ListAuditAsync(Guid id, int page, int size, CancellationToken ct) =>
        scanners.ListAuditAsync(id, page, size, ct);

    private static ValidationException InvalidInvitation() =>
        new([new("activation", "Mã ghép nối không hợp lệ, đã dùng hoặc đã hết hạn.")]);
}
