using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.ScannerDevices;
using WaterbusSystem.Application.DTOs.Responses.ScannerDevices;

namespace WaterbusSystem.Application.Interfaces.Services;

public interface IScannerDeviceAdminService
{
    Task<PaginatedList<ScannerDeviceResponse>> GetPageAsync(GetScannerDevicesRequest request, CancellationToken cancellationToken = default);
    Task<ScannerDeviceResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> RegisterAsync(RegisterScannerDeviceRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateScannerDeviceRequest request, CancellationToken cancellationToken = default);
    Task DeactivateAsync(Guid id, CancellationToken cancellationToken = default);
    Task ActivateAsync(Guid id, CancellationToken cancellationToken = default);
}
