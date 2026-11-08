using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.ScannerDevices;
using WaterbusSystem.Application.DTOs.Responses.ScannerDevices;
using WaterbusSystem.Domain.Entities;

namespace WaterbusSystem.Application.Interfaces.Repositories;

public interface IScannerDeviceAdminRepository
{
    Task<PaginatedList<ScannerDeviceResponse>> GetPageAsync(GetScannerDevicesRequest request, DateTimeOffset now, CancellationToken cancellationToken = default);

    Task<ScannerDeviceResponse> GetDetailAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>Thiết bị (có theo dõi) hoặc NotFound.</summary>
    Task<ScannerDevice> GetTrackedAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Mã đã dùng, kể cả bản ghi xoá mềm (unique index tính cả chúng).</summary>
    Task<bool> ExistsByCodeAsync(string deviceCode, CancellationToken cancellationToken = default);

    /// <summary>Số phân công của thiết bị chưa kết thúc tại <paramref name="now"/>.</summary>
    Task<int> CountActiveAssignmentsAsync(Guid deviceId, DateTimeOffset now, CancellationToken cancellationToken = default);

    void Add(ScannerDevice device);
}
