using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.Tracking;
using WaterbusSystem.Application.DTOs.Responses.Tracking;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Infrastructure.Persistence;

namespace WaterbusSystem.Infrastructure.Repositories;

public sealed class BoatPositionRepository(ApplicationDbContext context) : IBoatPositionRepository
{
    public Task<DateTimeOffset?> GetLastPersistedAtAsync(Guid boatId, CancellationToken cancellationToken = default) =>
        context.BoatPositionEvents.AsNoTracking()
            .Where(e => e.BoatId == boatId)
            .OrderByDescending(e => e.ObservedAt)
            .Select(e => (DateTimeOffset?)e.ObservedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<BoatPositionEvent?> GetLatestAsync(Guid boatId, CancellationToken cancellationToken = default) =>
        context.BoatPositionEvents.AsNoTracking()
            .Where(e => e.BoatId == boatId)
            .OrderByDescending(e => e.ObservedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public void Add(BoatPositionEvent positionEvent) => context.BoatPositionEvents.Add(positionEvent);

    public Task<PaginatedList<BoatPositionHistoryResponse>> GetHistoryPageAsync(
        GetBoatPositionHistoryRequest request, CancellationToken cancellationToken = default)
    {
        var query = context.BoatPositionEvents.AsNoTracking()
            .Where(e => e.BoatId == request.BoatId && e.ObservedAt >= request.From && e.ObservedAt <= request.To)
            .OrderBy(e => e.ObservedAt)
            .Select(e => new BoatPositionHistoryResponse(
                e.Id, e.TripId, e.Latitude, e.Longitude, e.SpeedKnots, e.ObservedAt, e.Source));

        return PaginatedList<BoatPositionHistoryResponse>.CreateAsync(query, request.PageNumber, request.PageSize, cancellationToken);
    }
}
