using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Interfaces;

namespace WaterbusSystem.Application.Features.Trips.Queries.GetTripOperationEvents;

public record TripOperationEventDto(
    Guid Id,
    Guid TripId,
    Guid? IncidentId,
    string Action,
    string? FromStatus,
    string ToStatus,
    string? Note,
    DateTimeOffset OccurredAt,
    string? ActedBy);

/// <summary>Nhật ký thao tác vận hành (TripOperationEvent), mới nhất trước; có thể lọc theo một chuyến.</summary>
public record GetTripOperationEventsQuery(Guid? TripId = null, int Take = 50) : IRequest<List<TripOperationEventDto>>;

public class GetTripOperationEventsQueryValidator : AbstractValidator<GetTripOperationEventsQuery>
{
    public GetTripOperationEventsQueryValidator()
    {
        RuleFor(x => x.Take).InclusiveBetween(1, 200).WithMessage("Số dòng phải trong [1, 200].");
    }
}

public class GetTripOperationEventsQueryHandler : IRequestHandler<GetTripOperationEventsQuery, List<TripOperationEventDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IUserDirectory _users;

    public GetTripOperationEventsQueryHandler(IApplicationDbContext context, IUserDirectory users)
    {
        _context = context;
        _users = users;
    }

    public async Task<List<TripOperationEventDto>> Handle(GetTripOperationEventsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.TripOperationEvents.AsNoTracking().Where(e => !e.IsDeleted);
        if (request.TripId is { } tripId) query = query.Where(e => e.TripId == tripId);

        var events = await query
            .OrderByDescending(e => e.OccurredAt)
            .Take(request.Take)
            .ToListAsync(cancellationToken);

        var actorIds = events
            .Select(e => e.ActedByAdminId ?? e.ActedByCaptainId)
            .OfType<Guid>()
            .Distinct()
            .ToList();
        var names = await _users.GetFullNamesAsync(actorIds, cancellationToken);

        return events.Select(e =>
        {
            var actor = e.ActedByAdminId ?? e.ActedByCaptainId;
            return new TripOperationEventDto(
                e.Id, e.TripId, e.IncidentId, e.Action, e.FromStatus, e.ToStatus, e.Note, e.OccurredAt,
                actor is { } id && names.TryGetValue(id, out var name) ? name : null);
        }).ToList();
    }
}
