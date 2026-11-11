using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Infrastructure.Persistence;

namespace WaterbusSystem.Infrastructure.Repositories;

public sealed class RouteRepository(ApplicationDbContext context) : IRouteRepository
{
    public Task<List<RouteStationPoints>> ListActiveWithStationPointsAsync(CancellationToken cancellationToken = default) =>
        ProjectAsync(context.Routes.AsNoTracking().Where(r => !r.IsDeleted && r.IsActive), cancellationToken);

    public Task<List<RouteStationPoints>> ListByIdsWithStationPointsAsync(
        IEnumerable<Guid> routeIds, CancellationToken cancellationToken = default)
    {
        var ids = routeIds.ToList();
        return ProjectAsync(context.Routes.AsNoTracking().Where(r => ids.Contains(r.Id) && !r.IsDeleted), cancellationToken);
    }

    // Lấy toạ độ các bến dừng của tuyến; tuyến chưa có điểm dừng thì dùng bến đi và bến đến
    private static async Task<List<RouteStationPoints>> ProjectAsync(IQueryable<Route> routes, CancellationToken cancellationToken)
    {
        var rows = await routes
            .OrderBy(r => r.Code)
            .Select(r => new
            {
                r.Id, 
                r.Code,
                r.Name,
                Stops = r.Stops.Where(s => !s.IsDeleted)
                    .Select(s => new { s.Station!.Latitude, s.Station.Longitude }).ToList(),
                Departure = new { r.DepartureStation!.Latitude, r.DepartureStation.Longitude },
                Arrival = new { r.ArrivalStation!.Latitude, r.ArrivalStation.Longitude }
            }).ToListAsync(cancellationToken);

        return rows.Select(r => new RouteStationPoints(
            r.Id, r.Code, r.Name,
            r.Stops.Count > 0 
                ? r.Stops.Select(s => new StationPoint(s.Latitude, s.Longitude)).ToList()
                : [new StationPoint(r.Departure.Latitude, r.Departure.Longitude),
                    new StationPoint(r.Arrival.Latitude, r.Arrival.Longitude)]
        )).ToList();
    }
}