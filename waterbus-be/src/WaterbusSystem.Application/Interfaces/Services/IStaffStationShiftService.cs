using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.ScannerDevices;
using WaterbusSystem.Application.DTOs.Responses.ScannerDevices;

namespace WaterbusSystem.Application.Interfaces.Services;

public interface IStaffStationShiftService
{
    Task<Guid> CreateAsync(CreateStaffShiftRequest request, CancellationToken ct);
    Task RevokeAsync(Guid id, CancellationToken ct);
    Task<PaginatedList<StaffStationShiftResponse>> ListAsync(int page, int size, CancellationToken ct);
}
