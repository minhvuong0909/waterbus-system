using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.Incidents;
using WaterbusSystem.Application.DTOs.Responses.Incidents;
using WaterbusSystem.Application.Features.Incidents;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure.Persistence;

namespace WaterbusSystem.Infrastructure.Repositories;

public sealed class IncidentRepository(ApplicationDbContext context) : IIncidentRepository
{
    public async Task<Incident> GetRequiredByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await context.Incidents.FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted, cancellationToken)
        ?? throw new NotFoundException(nameof(Incident), id);

    public async Task<IncidentDetailResponse>  GetDetailAsync(Guid id, CancellationToken cancellationToken = default) =>
        await context.Incidents.AsNoTracking()
            .Where(i => i.Id == id && !i.IsDeleted)
            .Select(i => new IncidentDetailResponse(
                i.Id, i.ScopeType, i.RouteId, i.Route != null ? i.Route.Code : null, i.BoatId,
                i.Source, i.IncidentType, i.Severity, i.Status, i.Description, i.OccurredAt, i.ResolvedAt,
                i.AffectedTrips.OrderBy(t => t.Trip!.DepartureTime)
                    .Select(t => new AffectedTripResponse(
                        t.Id, t.TripId, t.Trip!.DepartureTime, t.Trip.Status, t.Trip.BoatId,
                        t.OperationDecision, t.DecisionByAdminId, t.DecisionAt
                    )).ToList()
            )).FirstOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException(nameof(Incident), id);

    public Task<PaginatedList<IncidentSummaryResponse>> GetPageAsync(GetIncidentsRequest request, CancellationToken cancellationToken = default)
    {
        var query = context.Incidents.AsNoTracking().Where(i => !i.IsDeleted);

        if(!string.IsNullOrWhiteSpace(request.Status)) query = query.Where(i => i.Status == request.Status);
        if(!string.IsNullOrWhiteSpace(request.IncidentType)) query = query.Where(i => i.IncidentType == request.IncidentType);

        var projected = query
            .OrderByDescending(i => i.OccurredAt ?? i.CreatedAt)
            .Select(i => new IncidentSummaryResponse(
                i.Id,  i.ScopeType, i.RouteId, i.Route != null ? i.Route.Code : null, i.BoatId,
                i.Source, i.IncidentType, i.Severity, i.Status, i.Description, i.OccurredAt, i.ResolvedAt,
                i.AffectedTrips.Count, i.AffectedTrips.Count(t => t.OperationDecision == null)
            ));

        return PaginatedList<IncidentSummaryResponse>.CreateAsync(projected, request.PageNumber, request.PageSize, cancellationToken);
    }

    public Task<List<Incident>> ListOpenWeatherAsync(Guid? routeId = null, CancellationToken cancellationToken = default)
    {
        var query = context.Incidents
            .Include(i => i.AffectedTrips)
            .Where(i => !i.IsDeleted && i.Source == IncidentConstants.Sources.WeatherService
                                    && i.IncidentType == IncidentConstants.Types.Weather
                                    && i.Status == IncidentConstants.Statuses.Open);

        if(routeId.HasValue) query = query.Where(i => i.RouteId == routeId.Value);
        
        return query.OrderByDescending(i => i.OccurredAt).ToListAsync(cancellationToken);
    }

    public void Add(Incident incident) => context.Incidents.Add(incident);

    public void AddTripLink(IncidentTrip link) => context.IncidentTrips.Add(link);


}