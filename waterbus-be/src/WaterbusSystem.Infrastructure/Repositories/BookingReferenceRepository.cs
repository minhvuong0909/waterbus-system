using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Infrastructure.Persistence;

namespace WaterbusSystem.Infrastructure.Repositories;

public sealed class BookingReferenceRepository(
    ApplicationDbContext context) : IBookingReferenceRepository
{
    public Task<Trip?> GetTripWithStopsAsync(
        Guid tripId,
        CancellationToken ct = default)
    {
        return context.Trips
            .Include(t => t.Route)
            .Include(t => t.TripStopCalls)
                .ThenInclude(c => c.RouteStop)
            .FirstOrDefaultAsync(t => t.Id == tripId, ct);
    }

    public Task<List<Seat>> GetActiveSeatsAsync(
        IReadOnlyCollection<Guid> seatIds,
        CancellationToken ct = default)
    {
        var ids = seatIds.ToArray();

        return context.Seats
            .Include(s => s.SeatClass)
            .Where(s => ids.Contains(s.Id) && s.IsActive)
            .ToListAsync(ct);
    }

    public Task<List<FareRule>> GetEffectiveFaresAsync(
        TripType tripType,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        var type = tripType.ToString();

        return context.FareRules
            .AsNoTracking()
            .Where(f =>
                f.TripType == type &&
                f.IsActive &&
                f.EffectiveFrom <= now &&
                (f.EffectiveTo == null || f.EffectiveTo > now))
            .ToListAsync(ct);
    }
}
