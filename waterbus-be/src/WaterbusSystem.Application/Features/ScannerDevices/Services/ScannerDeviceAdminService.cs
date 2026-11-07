using ValidationException = WaterbusSystem.Application.Common.Exceptions.ValidationException;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.ScannerDevices;
using WaterbusSystem.Application.DTOs.Responses.ScannerDevices;
using WaterbusSystem.Application.Interfaces.Persistence;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Exceptions;

namespace WaterbusSystem.Application.Features.ScannerDevices.Services;

/// <summary>
/// Đăng ký/quản lý thiết bị Staff Scanner (BR-OFF-01/02, BR-CFG-01). Không xoá thiết bị và không đụng ScannerAssignment/CheckInEvent
/// (lịch sử kiểm toán giữ nguyên). Ngừng thiết bị còn phân công chưa kết thúc bị từ chối: Admin phải thu hồi phân công trước
/// (RevokeScannerAssignment) để không để lại phạm vi Trip + bến không có Primary Offline Scanner hợp lệ. Tuỳ chọn gán/thu hồi vẫn ở CheckIns.
/// </summary>
public sealed class ScannerDeviceAdminService(
    IScannerDeviceAdminRepository devices, IUnitOfWork unitOfWork, ICommercialTransactionRunner transactions, IDateTimeService dateTime)
    : IScannerDeviceAdminService
{
    public Task<PaginatedList<ScannerDeviceResponse>> GetPageAsync(GetScannerDevicesRequest request, CancellationToken cancellationToken = default) =>
        devices.GetPageAsync(request, dateTime.Now, cancellationToken);

    public Task<ScannerDeviceResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        devices.GetDetailAsync(id, dateTime.Now, cancellationToken);

    public Task<Guid> RegisterAsync(RegisterScannerDeviceRequest request, CancellationToken cancellationToken = default) =>
        transactions.RunAsync(async () =>
        {
            var code = request.DeviceCode.Trim();
            if (await devices.ExistsByCodeAsync(code, cancellationToken))
                throw new ValidationException([new("deviceCode", $"Mã thiết bị '{code}' đã tồn tại.")]);
            var device = new ScannerDevice { DeviceCode = code, Label = request.Label?.Trim(), IsActive = true };
            devices.Add(device);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return device.Id;
        }, cancellationToken);

    public async Task UpdateAsync(Guid id, UpdateScannerDeviceRequest request, CancellationToken cancellationToken = default)
    {
        var device = await devices.GetTrackedAsync(id, cancellationToken);
        device.Label = request.Label?.Trim();
        device.UpdatedAt = DateTimeOffset.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public Task DeactivateAsync(Guid id, CancellationToken cancellationToken = default) =>
        transactions.RunAsync(async () =>
        {
            var device = await devices.GetTrackedAsync(id, cancellationToken);
            if (!device.IsActive) return true;
            var active = await devices.CountActiveAssignmentsAsync(id, dateTime.Now, cancellationToken);
            if (active > 0)
                throw new ConflictException($"Thiết bị còn {active} phân công chưa kết thúc. Hãy thu hồi các phân công trước khi ngừng thiết bị.");
            device.IsActive = false;
            device.UpdatedAt = DateTimeOffset.UtcNow;
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }, cancellationToken);

    public async Task ActivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var device = await devices.GetTrackedAsync(id, cancellationToken);
        if (device.IsActive) return;
        device.IsActive = true;
        device.UpdatedAt = DateTimeOffset.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
