using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Common.Models;

namespace WaterbusSystem.Application.Features.Incidents.Queries.GetIncidents;

public record IncidentSummaryDto(
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
    int AffectedTripCount,
    int PendingDecisionCount);

/// <summary>
/// Danh sách Incident cho dashboard điều phối (mới nhất trước), lọc theo trạng thái / loại, có phân trang.
/// </summary>
public record GetIncidentsQuery(
    string? Status = null,
    string? IncidentType = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<PaginatedList<IncidentSummaryDto>>;

public class GetIncidentsQueryValidator : AbstractValidator<GetIncidentsQuery>
{
    public GetIncidentsQueryValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1).WithMessage("Số trang phải từ 1.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("Kích thước trang phải trong [1, 100].");
    }
}

public class GetIncidentsQueryHandler : IRequestHandler<GetIncidentsQuery, PaginatedList<IncidentSummaryDto>>
{
    private readonly IApplicationDbContext _context;

    public GetIncidentsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<IncidentSummaryDto>> Handle(GetIncidentsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Incidents.AsNoTracking().Where(i => !i.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.Status))
            query = query.Where(i => i.Status == request.Status);
        if (!string.IsNullOrWhiteSpace(request.IncidentType))
            query = query.Where(i => i.IncidentType == request.IncidentType);

        var projected = query
            .OrderByDescending(i => i.OccurredAt ?? i.CreatedAt)
            .Select(i => new IncidentSummaryDto(
                i.Id, i.ScopeType, i.RouteId, i.Route != null ? i.Route.Code : null, i.BoatId,
                i.Source, i.IncidentType, i.Severity, i.Status, i.Description, i.OccurredAt, i.ResolvedAt,
                i.AffectedTrips.Count,
                i.AffectedTrips.Count(t => t.OperationDecision == null)));

        return await PaginatedList<IncidentSummaryDto>.CreateAsync(projected, request.PageNumber, request.PageSize, cancellationToken);
    }
}
