using MediatR;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;

namespace WaterbusSystem.Application.Features.Incidents.Queries.GetIncidentById;

public record AffectedTripDto(
    Guid IncidentTripId,
    Guid TripId,
    DateTimeOffset DepartureTime,
    TripStatus TripStatus,
    Guid? BoatId,
    string? OperationDecision,
    Guid? DecisionByAdminId,
    DateTimeOffset? DecisionAt);

public record IncidentDetailDto(
    Guid Id,
    string ScopeType,
    Guid? RouteId,
    string? RouteCode,
    Guid? BoatId,
    string Source,
    string IncidentType,
    string? Severity,
    string Status,
    string? Description,
    DateTimeOffset? OccurredAt,
    DateTimeOffset? ResolvedAt,
    List<AffectedTripDto> AffectedTrips);

/// <summary>
/// Chi tiết Incident kèm các Trip bị ảnh hưởng và quyết định (nếu có) của Admin cho từng Trip.
/// </summary>
public record GetIncidentByIdQuery(Guid Id) : IRequest<IncidentDetailDto>;

public class GetIncidentByIdQueryHandler : IRequestHandler<GetIncidentByIdQuery, IncidentDetailDto>
{
    private readonly IApplicationDbContext _context;

    public GetIncidentByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IncidentDetailDto> Handle(GetIncidentByIdQuery request, CancellationToken cancellationToken)
    {
        return await _context.Incidents.AsNoTracking()
            .Where(i => i.Id == request.Id && !i.IsDeleted)
            .Select(i => new IncidentDetailDto(
                i.Id, i.ScopeType, i.RouteId, i.Route != null ? i.Route.Code : null, i.BoatId,
                i.Source, i.IncidentType, i.Severity, i.Status, i.Description, i.OccurredAt, i.ResolvedAt,
                i.AffectedTrips
                    .OrderBy(t => t.Trip!.DepartureTime)
                    .Select(t => new AffectedTripDto(
                        t.Id, t.TripId, t.Trip!.DepartureTime, t.Trip.Status, t.Trip.BoatId,
                        t.OperationDecision, t.DecisionByAdminId, t.DecisionAt))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Incident), request.Id);
    }
}
