using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Infrastructure.Persistence;

namespace WaterbusSystem.Infrastructure.Repositories;

public sealed class TripRepository(ApplicationDbContext context) : ITripRepository
{
    public Task<List<ActiveTripRef>> ListActiveInWindowAsync(
        IReadOnlyCollection<TripStatus> statuses, DateTimeOffset now, DateTimeOffset horizon,
        CancellationToken cancellationToken = default) =>
        context.Trips.AsNoTracking()
            .Where(t => !t.IsDeleted
                        && statuses.Contains(t.Status)
                        && t.DepartureTime <= horizon
                        && t.ArrivalTime >= now)
            .Select(t => new ActiveTripRef(t.Id, t.RouteId))
            .ToListAsync(cancellationToken);
}

