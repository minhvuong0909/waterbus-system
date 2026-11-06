using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.Routes;
using WaterbusSystem.Application.DTOs.Responses.Routes;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure.Persistence;

namespace WaterbusSystem.Infrastructure.Repositories;

public sealed class RouteAdminRepository(ApplicationDbContext context) : IRouteAdminRepository
{
    public Task<PaginatedList<RouteListItemResponse>> GetPageAsync(GetRoutesRequest request, CancellationToken cancellationToken = default)
    {
        var query = context.Routes.AsNoTracking().Where(r => !r.IsDeleted);
        if (request.IsActive.HasValue) query = query.Where(r => r.IsActive == request.IsActive.Value);
        if (request.ServiceType != null) query = query.Where(r => r.ServiceType == request.ServiceType);

        return query.OrderBy(r => r.Code)
            .Select(r => new RouteListItemResponse(r.Id, r.Code, r.Name, r.ServiceType, r.IsActive, r.EstimatedDurationMinutes, r.DistanceKm,
                r.DepartureStation!.Name, r.ArrivalStation!.Name, r.Stops.Count(s => !s.IsDeleted)))
            .ToPaginatedListAsync(request.PageNumber, request.PageSize, cancellationToken);
    }

    public async Task<RouteDetailResponse> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var route = await context.Routes.AsNoTracking()
            .Include(r => r.Stops.Where(s => !s.IsDeleted).OrderBy(s => s.SequenceNo)).ThenInclude(s => s.Station)
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Route), id);
        var locked = await IsReferencedAsync(id, cancellationToken);
        return new(route.Id, route.Code, route.Name, route.ServiceType, route.IsActive, route.EstimatedDurationMinutes, route.DistanceKm, locked,
            route.Stops.Select(s => new RouteStopResponse(s.Id, s.SequenceNo, s.StationId, s.Station?.Name ?? "", s.CanBoard, s.CanAlight)).ToList());
    }

    public async Task<Route> GetTrackedWithStopsAsync(Guid id, CancellationToken cancellationToken = default) =>
        await context.Routes.Include(r => r.Stops.OrderBy(s => s.SequenceNo)).IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted, cancellationToken)
        ?? throw new NotFoundException(nameof(Route), id);

    public Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        context.Routes.IgnoreQueryFilters().AnyAsync(r => r.Code == code, cancellationToken);

    public Task<List<Station>> GetActiveStationsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) =>
        context.Stations.Where(s => ids.Contains(s.Id) && !s.IsDeleted && s.IsActive).ToListAsync(cancellationToken);

    public async Task<bool> IsReferencedAsync(Guid routeId, CancellationToken cancellationToken = default) =>
        await context.Schedules.IgnoreQueryFilters().AnyAsync(s => s.RouteId == routeId, cancellationToken)
        || await context.Trips.IgnoreQueryFilters().AnyAsync(t => t.RouteId == routeId, cancellationToken);

    public async Task<bool> HasLiveDependentsAsync(Guid routeId, CancellationToken cancellationToken = default) =>
        await context.Schedules.AnyAsync(s => s.RouteId == routeId && s.IsActive, cancellationToken)
        || await context.Trips.AnyAsync(t => t.RouteId == routeId
            && t.Status != TripStatus.Completed && t.Status != TripStatus.Cancelled && t.Status != TripStatus.Terminated, cancellationToken);

    public void Add(Route route) => context.Routes.Add(route);

    public void AddStop(RouteStop stop) => context.RouteStops.Add(stop);
}
