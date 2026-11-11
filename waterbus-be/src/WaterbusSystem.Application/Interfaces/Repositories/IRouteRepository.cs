using WaterbusSystem.Application.Common.Models;

namespace WaterbusSystem.Application.Interfaces.Repositories;

public interface IRouteRepository
{
    /// <summary>Mọi tuyến đang hoạt động, sắp theo mã tuyến.</summary>
    Task<List<RouteStationPoints>> ListActiveWithStationPointsAsync(CancellationToken cancellationToken = default);

    /// <summary>Các tuyến (chưa xoá) theo danh sách Id.</summary>
    Task<List<RouteStationPoints>> ListByIdsWithStationPointsAsync(
        IEnumerable<Guid> routeIds, CancellationToken cancellationToken = default);
}
