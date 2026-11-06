using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;
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

    public Task<Guid?> FindLatestTripIdByBoatAsync(
        Guid boatId, IReadOnlyCollection<TripStatus> statuses, CancellationToken cancellationToken = default) =>
        context.Trips.AsNoTracking()
            .Where(t => t.BoatId == boatId && !t.IsDeleted && statuses.Contains(t.Status))
            .OrderByDescending(t => t.DepartureTime)
            .Select(t => (Guid?)t.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<Trip> GetDetailGraphAsync(Guid tripId, CancellationToken cancellationToken = default)
    {
        var trip = await context.Trips.AsNoTracking()
            .Include(t => t.Route).Include(t => t.Boat)
            .Include(t => t.TripStopCalls).ThenInclude(c => c.RouteStop).ThenInclude(s => s!.Station)
            .FirstOrDefaultAsync(t => t.Id == tripId, cancellationToken)
            ?? throw new NotFoundException(nameof(Trip), tripId);

        // Không xuất điểm dừng bị xóa mềm hay mất dữ liệu danh mục.
        trip.TripStopCalls = trip.TripStopCalls.Where(c => !c.IsDeleted && c.RouteStop is { IsDeleted: false }
            && c.RouteStop.Station is { IsDeleted: false }).ToList();
        return trip;
    }

    public Task<List<Seat>> ListSeatsForBoatAsync(Guid boatId, CancellationToken cancellationToken = default) =>
        context.Seats.AsNoTracking().Include(s => s.SeatClass)
            .Where(s => s.BoatId == boatId)
            .OrderBy(s => s.RowNumber).ThenBy(s => s.ColumnNumber).ThenBy(s => s.SeatCode)
            .ToListAsync(cancellationToken);

    public Task<List<FareRule>> ListActiveFaresForTypeAsync(string tripType, DateTimeOffset now, CancellationToken cancellationToken = default) =>
        context.FareRules.AsNoTracking()
            .Where(f => f.TripType == tripType && f.IsActive && f.EffectiveFrom <= now && (f.EffectiveTo == null || f.EffectiveTo > now))
            .ToListAsync(cancellationToken);

    public Task<List<SeatReservationOverlap>> ListOverlappingReservationsAsync(
        Guid tripId, int boardingVisitOrder, int disembarkingVisitOrder, CancellationToken cancellationToken = default) =>
        context.SeatReservations.AsNoTracking()
            .Where(r => r.TripId == tripId && r.Status != ReservationStatus.Cancelled
                && boardingVisitOrder < r.DisembarkingStopOrder && disembarkingVisitOrder > r.BoardingStopOrder)
            .Select(r => new SeatReservationOverlap(r.SeatId, r.Status, r.Booking!.Order!.ExpiresAt))
            .ToListAsync(cancellationToken);

    public Task<List<Trip>> ListForOperationsAsync(
        DateTimeOffset from, DateTimeOffset to, Guid? captainId, IReadOnlyList<TripStatus>? statuses, int take,
        CancellationToken cancellationToken = default)
    {
        var query = context.Trips.AsNoTracking()
            .Include(t => t.Route).Include(t => t.Boat)
            .Where(t => !t.IsDeleted && t.DepartureTime >= from && t.DepartureTime < to);

        if (captainId.HasValue)
        {
            query = query.Where(t => t.Boat != null && t.Boat.CaptainUserId == captainId.Value);
        }
        if (statuses is { Count: > 0 })
        {
            query = query.Where(t => statuses.Contains(t.Status));
        }

        return query.OrderBy(t => t.DepartureTime).Take(take).ToListAsync(cancellationToken);
    }

    public Task<List<TripOperationEvent>> ListOperationEventsAsync(
        Guid? tripId, int take, CancellationToken cancellationToken = default)
    {
        var query = context.TripOperationEvents.AsNoTracking().Where(e => !e.IsDeleted);
        if (tripId is { } id) query = query.Where(e => e.TripId == id);

        return query.OrderByDescending(e => e.OccurredAt).Take(take).ToListAsync(cancellationToken);
    }

    public async Task<Trip> GetTrackedWithBoatAsync(Guid tripId, CancellationToken cancellationToken = default) =>
        await context.Trips.Include(t => t.Boat).Include(t => t.TripStopCalls)
            .FirstOrDefaultAsync(t => t.Id == tripId && !t.IsDeleted, cancellationToken)
        ?? throw new NotFoundException(nameof(Trip), tripId);

    public async Task<TripStopCall> GetTrackedStopCallAsync(Guid tripId, Guid tripStopCallId, CancellationToken cancellationToken = default) =>
        await context.TripStopCalls
            .Include(c => c.Trip).ThenInclude(t => t!.Boat)
            .Include(c => c.Trip).ThenInclude(t => t!.TripStopCalls)
            .FirstOrDefaultAsync(c => c.Id == tripStopCallId && c.TripId == tripId, cancellationToken)
        ?? throw new NotFoundException(nameof(TripStopCall), tripStopCallId);

    public async Task<Trip> GetWithBoatAsync(Guid tripId, CancellationToken cancellationToken = default) =>
        await context.Trips.AsNoTracking().Include(t => t.Boat)
            .FirstOrDefaultAsync(t => t.Id == tripId && !t.IsDeleted, cancellationToken)
        ?? throw new NotFoundException(nameof(Trip), tripId);

    public void AddOperationEvent(TripOperationEvent operationEvent) => context.TripOperationEvents.Add(operationEvent);

    public Task<List<Trip>> SearchSellableAsync(
        DateTimeOffset? departureDayStartUtc, DateTimeOffset? departureDayEndUtc, TripType? tripType, DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var query = context.Trips.AsNoTracking()
            .Include(t => t.Route).Include(t => t.Boat)
            .Include(t => t.TripStopCalls).ThenInclude(c => c.RouteStop).ThenInclude(rs => rs!.Station)
            .Where(t => !t.IsDeleted && (t.Status == TripStatus.Scheduled || t.Status == TripStatus.Boarding)
                        && t.ActualDepartureTime == null && t.BoatId != null
                        && (t.SalesCloseAt == null || t.SalesCloseAt > now));

        if (departureDayStartUtc.HasValue && departureDayEndUtc.HasValue)
        {
            query = query.Where(t => t.DepartureTime >= departureDayStartUtc.Value && t.DepartureTime < departureDayEndUtc.Value);
        }
        if (tripType.HasValue)
        {
            query = query.Where(t => t.TripType == tripType.Value);
        }

        return query.OrderBy(t => t.DepartureTime).ToListAsync(cancellationToken);
    }

    public Task<List<SeatReservationSegment>> ListReservationSegmentsAsync(
        IReadOnlyList<Guid> tripIds, CancellationToken cancellationToken = default) =>
        context.SeatReservations.AsNoTracking()
            .Where(r => tripIds.Contains(r.TripId) && r.Status != ReservationStatus.Cancelled)
            .Select(r => new SeatReservationSegment(r.TripId, r.SeatId, r.BoardingStopOrder, r.DisembarkingStopOrder))
            .ToListAsync(cancellationToken);

    public Task<List<Seat>> ListSeatsForBoatsAsync(IReadOnlyCollection<Guid> boatIds, CancellationToken cancellationToken = default) =>
        context.Seats.AsNoTracking().Include(s => s.SeatClass)
            .Where(s => boatIds.Contains(s.BoatId))
            .ToListAsync(cancellationToken);

    public Task<List<FareRule>> ListActiveFaresAsync(DateTimeOffset now, CancellationToken cancellationToken = default) =>
        context.FareRules.AsNoTracking()
            .Where(f => f.IsActive && f.EffectiveFrom <= now && (f.EffectiveTo == null || f.EffectiveTo > now))
            .ToListAsync(cancellationToken);
}
