using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.Routes;
using WaterbusSystem.Application.DTOs.Responses.Routes;
using WaterbusSystem.Domain.Entities;

namespace WaterbusSystem.Application.Interfaces.Repositories;

public interface IRouteAdminRepository
{
    Task<PaginatedList<RouteListItemResponse>> GetPageAsync(GetRoutesRequest request, CancellationToken cancellationToken = default);

    Task<RouteDetailResponse> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Route (có theo dõi) kèm MỌI RouteStop kể cả xoá mềm (unique (RouteId, SequenceNo) tính cả chúng), hoặc NotFound.</summary>
    Task<Route> GetTrackedWithStopsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Mã đã dùng, kể cả bản ghi xoá mềm.</summary>
    Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>Station đang hoạt động (chưa xoá) trong danh sách.</summary>
    Task<List<Station>> GetActiveStationsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>Có Schedule hoặc Trip (kể cả đã xoá mềm) tham chiếu Route.</summary>
    Task<bool> IsReferencedAsync(Guid routeId, CancellationToken cancellationToken = default);

    /// <summary>Có Schedule đang hoạt động hoặc Trip chưa đóng (mọi trạng thái trừ Completed/Cancelled/Terminated, kể cả Arrived chờ Complete) trên Route.</summary>
    Task<bool> HasLiveDependentsAsync(Guid routeId, CancellationToken cancellationToken = default);

    void Add(Route route);

    void AddStop(RouteStop stop);
}
