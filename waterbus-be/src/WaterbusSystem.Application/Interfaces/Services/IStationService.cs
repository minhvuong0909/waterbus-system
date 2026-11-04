using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.Stations;
using WaterbusSystem.Application.DTOs.Responses.Stations;

namespace WaterbusSystem.Application.Interfaces.Services;

public interface IStationService
{
    Task<Guid> CreateAsync(CreateStationRequest request, CancellationToken cancellationToken = default);

    Task UpdateAsync(Guid id, UpdateStationRequest request, CancellationToken cancellationToken = default);

    Task<StationResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Toàn bộ bến đang hoạt động, sắp theo thứ tự luồng sông (OrderIndex). Công khai, không phân trang.</summary>
    Task<List<StationResponse>> GetActiveStationsAsync(CancellationToken cancellationToken = default);

    Task<PaginatedList<StationResponse>> GetPageAsync(GetStationsPagedRequest request, CancellationToken cancellationToken = default);
}
