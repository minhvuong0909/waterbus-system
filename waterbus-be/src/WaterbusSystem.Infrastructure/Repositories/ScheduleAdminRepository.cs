using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.Schedules;
using WaterbusSystem.Application.DTOs.Responses.Schedules;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure.Persistence;

namespace WaterbusSystem.Infrastructure.Repositories;

public sealed class ScheduleAdminRepository(ApplicationDbContext context) : IScheduleAdminRepository
{
    public Task<PaginatedList<ScheduleListItemResponse>> GetPageAsync(GetSchedulesRequest request, CancellationToken cancellationToken = default)
    {
        var query = context.Schedules.AsNoTracking().Where(s => !s.IsDeleted);
        if (request.RouteId.HasValue) query = query.Where(s => s.RouteId == request.RouteId.Value);
        if (request.IsActive.HasValue) query = query.Where(s => s.IsActive == request.IsActive.Value);

        return query.OrderBy(s => s.Route!.Code).ThenBy(s => s.DepartureTime)
            .Select(s => new ScheduleListItemResponse(s.Id, s.RouteId, s.Route!.Code, s.Route.Name, s.TripType.ToString(), s.Direction,
                s.DepartureTime, s.DaysOfWeek, s.EffectiveFrom, s.EffectiveTo, s.IsActive, s.Stops.Count(x => !x.IsDeleted)))
            .ToPaginatedListAsync(request.PageNumber, request.PageSize, cancellationToken);
    }

    public async Task<ScheduleDetailResponse> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var s = await context.Schedules.AsNoTracking().Include(x => x.Route)
            .Include(x => x.Stops.Where(p => !p.IsDeleted).OrderBy(p => p.VisitOrder)).ThenInclude(p => p.RouteStop!).ThenInclude(r => r.Station)
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Schedule), id);
        return new(s.Id, s.RouteId, s.Route!.Code, s.Route.Name, s.TripType.ToString(), s.Direction, s.DepartureTime, s.DaysOfWeek,
            s.EffectiveFrom, s.EffectiveTo, s.IsActive, await HasTripsAsync(id, cancellationToken),
            s.Stops.Select(p => new ScheduleStopResponse(p.Id, p.RouteStopId, p.VisitOrder, p.RouteStop!.SequenceNo, p.RouteStop.StationId,
                p.RouteStop.Station?.Name ?? "", p.ArrivalOffsetMin, p.DepartureOffsetMin)).ToList());
    }

    public async Task<Schedule> GetTrackedWithStopsAsync(Guid id, CancellationToken cancellationToken = default) =>
        await context.Schedules.Include(s => s.Stops.OrderBy(p => p.VisitOrder)).IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, cancellationToken)
        ?? throw new NotFoundException(nameof(Schedule), id);

    public async Task<Route> GetRouteWithStopsAsync(Guid routeId, CancellationToken cancellationToken = default) =>
        await context.Routes.AsNoTracking().Include(r => r.Stops.Where(s => !s.IsDeleted))
            .FirstOrDefaultAsync(r => r.Id == routeId && !r.IsDeleted, cancellationToken)
        ?? throw new NotFoundException(nameof(Route), routeId);

    public Task<List<Schedule>> ListActiveSiblingsAsync(
        Guid routeId, TripType tripType, string direction, TimeSpan departureTime, Guid? excludeId, CancellationToken cancellationToken = default) =>
        context.Schedules.AsNoTracking()
            .Where(s => s.RouteId == routeId && s.IsActive && s.Direction == direction && s.DepartureTime == departureTime
                        && s.Id != excludeId && s.TripType == tripType)
            .ToListAsync(cancellationToken);

    public Task<bool> HasTripsAsync(Guid scheduleId, CancellationToken cancellationToken = default) =>
        context.Trips.IgnoreQueryFilters().AnyAsync(t => t.ScheduleId == scheduleId, cancellationToken);

    public void Add(Schedule schedule) => context.Schedules.Add(schedule);

    public void AddStop(ScheduleStop stop) => context.ScheduleStops.Add(stop);
}
