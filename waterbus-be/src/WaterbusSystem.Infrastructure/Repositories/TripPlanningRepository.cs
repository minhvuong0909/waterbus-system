using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure.Persistence;

namespace WaterbusSystem.Infrastructure.Repositories;

public sealed class TripPlanningRepository(ApplicationDbContext context) : ITripPlanningRepository
{
    public async Task<Schedule> GetScheduleForGenerationAsync(Guid scheduleId, CancellationToken cancellationToken = default) =>
        await context.Schedules.AsNoTracking().Include(s => s.Route)
            .Include(s => s.Stops.Where(p => !p.IsDeleted)).ThenInclude(p => p.RouteStop)
            .FirstOrDefaultAsync(s => s.Id == scheduleId && !s.IsDeleted, cancellationToken)
        ?? throw new NotFoundException(nameof(Schedule), scheduleId);

    public async Task<HashSet<DateTimeOffset>> ListExistingDeparturesAsync(
        Guid scheduleId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default) =>
        (await context.Trips.IgnoreQueryFilters().AsNoTracking()
            .Where(t => t.ScheduleId == scheduleId && t.DepartureTime >= fromUtc && t.DepartureTime <= toUtc)
            .Select(t => t.DepartureTime).ToListAsync(cancellationToken)).ToHashSet();

    public async Task<Route> GetRouteWithStopsAsync(Guid routeId, CancellationToken cancellationToken = default) =>
        await context.Routes.AsNoTracking().Include(r => r.Stops.Where(s => !s.IsDeleted))
            .FirstOrDefaultAsync(r => r.Id == routeId && !r.IsDeleted, cancellationToken)
        ?? throw new NotFoundException(nameof(Route), routeId);

    public void AddTrip(Trip trip) => context.Trips.Add(trip);

    public async Task<Trip> GetTripTrackedAsync(Guid tripId, CancellationToken cancellationToken = default) =>
        await context.Trips.FirstOrDefaultAsync(t => t.Id == tripId && !t.IsDeleted, cancellationToken)
        ?? throw new NotFoundException(nameof(Trip), tripId);

    public async Task<Boat> GetBoatWithActiveSeatsAsync(Guid boatId, CancellationToken cancellationToken = default) =>
        await context.Boats.AsNoTracking()
            .Include(b => b.Seats.Where(s => s.IsActive && !s.IsDeleted)).ThenInclude(s => s.SeatClass)
            .FirstOrDefaultAsync(b => b.Id == boatId && !b.IsDeleted, cancellationToken)
        ?? throw new NotFoundException(nameof(Boat), boatId);

    public Task<bool> HasOverlappingTripAsync(
        Guid boatId, DateTimeOffset from, DateTimeOffset to, Guid excludeTripId, CancellationToken cancellationToken = default) =>
        context.Trips.AsNoTracking().AnyAsync(t => t.BoatId == boatId && t.Id != excludeTripId
            && t.Status != TripStatus.Cancelled && t.Status != TripStatus.Terminated
            && t.DepartureTime < to && from < t.ArrivalTime, cancellationToken);

    public Task<List<SeatReservation>> ListLiveReservationsTrackedAsync(Guid tripId, CancellationToken cancellationToken = default) =>
        context.SeatReservations.Include(r => r.Seat)
            .Where(r => r.TripId == tripId && (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Confirmed))
            .ToListAsync(cancellationToken);
}
