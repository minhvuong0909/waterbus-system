using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.Routes;
using WaterbusSystem.Application.DTOs.Responses.Routes;

namespace WaterbusSystem.Application.Interfaces.Services;

public interface IRouteAdminService
{
    Task<PaginatedList<RouteListItemResponse>> GetPageAsync(GetRoutesRequest request, CancellationToken cancellationToken = default);
    Task<RouteDetailResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateRouteRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateRouteRequest request, CancellationToken cancellationToken = default);
    Task<RouteDetailResponse> ReplaceStopsAsync(Guid id, ReplaceRouteStopsRequest request, CancellationToken cancellationToken = default);
    Task DeactivateAsync(Guid id, CancellationToken cancellationToken = default);
    Task ActivateAsync(Guid id, CancellationToken cancellationToken = default);
}
