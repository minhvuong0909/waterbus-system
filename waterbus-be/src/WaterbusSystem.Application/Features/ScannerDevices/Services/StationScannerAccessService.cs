using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Responses.ScannerDevices;
using WaterbusSystem.Application.Interfaces.Persistence;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Exceptions;

namespace WaterbusSystem.Application.Features.ScannerDevices.Services;

public sealed class StationScannerAccessService(
    ICurrentUserService user, ICheckInRepository checkIns, IStationScannerRepository scanners,
    IScannerDeviceAdminRepository devices, IScannerAssignmentLock locks, IScannerSecrets secrets)
{
    internal static readonly TimeSpan LockWait = TimeSpan.FromSeconds(10);

    public async Task<Guid> RequireStaffAsync(CancellationToken ct)
    {
        if (!Guid.TryParse(user.UserId, out var id) || !string.Equals(user.UserRole, "Staff", StringComparison.OrdinalIgnoreCase)
            || !await checkIns.IsEligibleScannerStaffAsync(id, ct))
            throw new ForbiddenAccessException("Yêu cầu tài khoản Staff đang hoạt động.");
        return id;
    }

    public async Task<ScannerAccess> OpenAsync(string? credential, bool allowHistorical, CancellationToken ct)
    {
        var staffId = await RequireStaffAsync(ct);
        if (!TryParseSecret(credential, out var enrollmentId, out var secret))
            throw InvalidDevice();
        var enrollment = await scanners.FindEnrollmentAsync(enrollmentId, ct);
        if (enrollment is null || !secrets.Matches(secret, enrollment.CredentialHash))
            throw InvalidDevice();

        var deviceGate = await LockDeviceAsync(enrollment.ScannerDeviceId, ct);
        try
        {
            var staffGate = await LockStaffAsync(staffId, ct);
            try
            {
                await RequireStaffAsync(ct);
                enrollment = await scanners.FindEnrollmentAsync(enrollmentId, ct) ?? throw InvalidDevice();
                var device = await devices.GetTrackedAsync(enrollment.ScannerDeviceId, ct);
                var historical = enrollment.EndedAt is not null;
                if (historical ? !allowHistorical : !device.IsActive || device.StationId != enrollment.StationId)
                    throw InvalidDevice();

                var stationName = historical ? "" : (await scanners.GetStationAsync(enrollment.StationId, ct)).Name;
                var identity = new ScannerIdentityResponse(device.Id, enrollment.Id, device.DeviceCode, enrollment.StationId, stationName);
                return new ScannerAccess(identity, staffId, enrollment, deviceGate, staffGate);
            }
            catch
            {
                await staffGate.DisposeAsync();
                throw;
            }
        }
        catch
        {
            await deviceGate.DisposeAsync();
            throw;
        }
    }

    internal async Task<IAsyncDisposable> LockDeviceAsync(Guid id, CancellationToken ct) =>
        await locks.TryAcquireDeviceAsync(id, LockWait, ct)
        ?? throw new ConflictException("Thiết bị đang xử lý một yêu cầu khác. Vui lòng thử lại.");

    internal async Task<IAsyncDisposable> LockStaffAsync(Guid id, CancellationToken ct) =>
        await locks.TryAcquireStaffAsync(id, LockWait, ct)
        ?? throw new ConflictException("Ca làm việc đang được xử lý. Vui lòng thử lại.");

    internal static bool TryParseSecret(string? value, out Guid id, out string secret)
    {
        id = Guid.Empty;
        secret = "";
        if (value?.Length != 97 || value[32] != '.' || !Guid.TryParseExact(value[..32], "N", out id)) return false;
        secret = value[33..];
        return secret.All(char.IsAsciiHexDigitUpper);
    }

    private static ForbiddenAccessException InvalidDevice() =>
        new("Thiết bị chưa được ghép nối hoặc thông tin ghép nối đã bị thu hồi.");
}

public sealed class ScannerAccess : IAsyncDisposable
{
    private readonly IAsyncDisposable _deviceGate;
    private readonly IAsyncDisposable _staffGate;

    public ScannerIdentityResponse Scanner { get; }
    public Guid StaffId { get; }
    public ScannerEnrollment Enrollment { get; }

    internal ScannerAccess(ScannerIdentityResponse scanner, Guid staffId, ScannerEnrollment enrollment,
        IAsyncDisposable deviceGate, IAsyncDisposable staffGate)
    {
        Scanner = scanner;
        StaffId = staffId;
        Enrollment = enrollment;
        _deviceGate = deviceGate;
        _staffGate = staffGate;
    }

    public async ValueTask DisposeAsync()
    {
        try { await _staffGate.DisposeAsync(); }
        finally { await _deviceGate.DisposeAsync(); }
    }
}
