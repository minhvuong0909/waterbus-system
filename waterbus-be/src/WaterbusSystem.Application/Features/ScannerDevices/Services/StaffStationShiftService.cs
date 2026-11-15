using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.ScannerDevices;
using WaterbusSystem.Application.DTOs.Responses.ScannerDevices;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Application.Interfaces.Persistence;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Exceptions;
using ValidationException = WaterbusSystem.Application.Common.Exceptions.ValidationException;

namespace WaterbusSystem.Application.Features.ScannerDevices.Services;

public sealed class StaffStationShiftService(
    IStationScannerRepository scanners, ICheckInRepository checkIns, StationScannerAccessService access,
    IDateTimeService clock, IUnitOfWork unitOfWork) : IStaffStationShiftService
{
    public async Task<Guid> CreateAsync(CreateStaffShiftRequest request, CancellationToken ct)
    {
        await using var gate = await access.LockStaffAsync(request.StaffAccountId, ct);
        var now = clock.Now;
        if (request.ActiveFrom >= request.ActiveUntil || request.ActiveUntil <= now)
            throw new ValidationException([new("activeUntil", "Ca làm việc phải có thời gian kết thúc hợp lệ trong tương lai.")]);
        if (!await checkIns.IsEligibleScannerStaffAsync(request.StaffAccountId, ct))
            throw new ValidationException([new("staffAccountId", "Chọn tài khoản Staff đang hoạt động.")]);
        await scanners.GetStationAsync(request.StationId, ct);

        // An ongoing shift can start now. Creation cannot authorize a scan that already happened.
        var starts = request.ActiveFrom < now ? now : request.ActiveFrom;
        if (await scanners.OverlapsShiftAsync(request.StaffAccountId, starts, request.ActiveUntil, ct))
            throw new ConflictException("Ca làm việc trùng với ca khác của nhân viên.");

        var shift = new StaffStationShift { StaffAccountId = request.StaffAccountId, StationId = request.StationId,
            ActiveFrom = starts.ToUniversalTime(), ActiveUntil = request.ActiveUntil.ToUniversalTime() };
        scanners.AddShift(shift);
        await unitOfWork.SaveChangesAsync(ct);
        return shift.Id;
    }

    public async Task RevokeAsync(Guid id, CancellationToken ct)
    {
        var owner = await scanners.GetShiftAsync(id, tracked: false, ct);
        await using var gate = await access.LockStaffAsync(owner.StaffAccountId, ct);
        var shift = await scanners.GetShiftAsync(id, tracked: true, ct);
        shift.Revoke(clock.Now);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public Task<PaginatedList<StaffStationShiftResponse>> ListAsync(int page, int size, CancellationToken ct) =>
        scanners.ListShiftsAsync(page, size, ct);
}
