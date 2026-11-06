using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.DTOs.Responses.SeatInventory;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure.Persistence;

namespace WaterbusSystem.Infrastructure.Repositories;

public sealed class BoatSeatRepository(ApplicationDbContext context) : IBoatSeatRepository
{
    public async Task<Boat> GetBoatForUpdateAsync(Guid boatId, CancellationToken cancellationToken = default) =>
        await context.Boats.FirstOrDefaultAsync(b => b.Id == boatId && !b.IsDeleted, cancellationToken)
        ?? throw new NotFoundException(nameof(Boat), boatId);

    public Task<List<Seat>> ListAllTrackedAsync(Guid boatId, CancellationToken cancellationToken = default) =>
        // IgnoreQueryFilters: unique (BoatId, SeatCode) tính cả ghế xoá mềm nên kiểm trùng phải thấy chúng.
        context.Seats.IgnoreQueryFilters().Where(s => s.BoatId == boatId).ToListAsync(cancellationToken);

    public Task<List<BoatSeatResponse>> ListAsync(Guid boatId, CancellationToken cancellationToken = default) =>
        context.Seats.AsNoTracking()
            .Where(s => s.BoatId == boatId && !s.IsDeleted)
            .OrderBy(s => s.RowNumber).ThenBy(s => s.ColumnNumber).ThenBy(s => s.SeatCode)
            .Select(s => new BoatSeatResponse(s.Id, s.BoatId, s.SeatCode, s.SeatClassId,
                s.SeatClass!.Code, s.SeatClass.Name, s.RowNumber, s.ColumnNumber, s.IsActive))
            .ToListAsync(cancellationToken);

    public Task<List<SeatClass>> GetClassesAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) =>
        context.SeatClasses.Where(c => ids.Contains(c.Id) && !c.IsDeleted).ToListAsync(cancellationToken);

    public async Task<HashSet<Guid>> GetSeatIdsWithOpenCommitmentsAsync(IReadOnlyCollection<Guid> seatIds, CancellationToken cancellationToken = default) =>
        (await context.SeatReservations.AsNoTracking()
            .Where(r => seatIds.Contains(r.SeatId)
                && (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Confirmed)
                && r.Trip!.Status != TripStatus.Arrived && r.Trip.Status != TripStatus.Completed
                && r.Trip.Status != TripStatus.Cancelled && r.Trip.Status != TripStatus.Terminated)
            .Select(r => r.SeatId).Distinct().ToListAsync(cancellationToken)).ToHashSet();

    public void AddRange(IEnumerable<Seat> seats) => context.Seats.AddRange(seats);
}
