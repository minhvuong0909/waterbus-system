using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.DTOs.Responses.SeatInventory;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure.Persistence;

namespace WaterbusSystem.Infrastructure.Repositories;

public sealed class SeatClassRepository(ApplicationDbContext context) : ISeatClassRepository
{
    public Task<List<SeatClassResponse>> ListAsync(bool includeInactive, CancellationToken cancellationToken = default) =>
        context.SeatClasses.AsNoTracking()
            .Where(c => !c.IsDeleted && (includeInactive || c.IsActive))
            .OrderBy(c => c.Code)
            .Select(c => new SeatClassResponse(c.Id, c.Code, c.Name, c.Description, c.IsActive, c.Seats.Count(s => !s.IsDeleted)))
            .ToListAsync(cancellationToken);

    public async Task<SeatClassResponse> GetDetailAsync(Guid id, CancellationToken cancellationToken = default) =>
        await context.SeatClasses.AsNoTracking().Where(c => c.Id == id && !c.IsDeleted)
            .Select(c => new SeatClassResponse(c.Id, c.Code, c.Name, c.Description, c.IsActive, c.Seats.Count(s => !s.IsDeleted)))
            .FirstOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException(nameof(SeatClass), id);

    public async Task<SeatClass> GetRequiredByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await context.SeatClasses.FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, cancellationToken)
        ?? throw new NotFoundException(nameof(SeatClass), id);

    public Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        context.SeatClasses.IgnoreQueryFilters().AnyAsync(c => c.Code == code, cancellationToken);

    public Task<bool> HasOpenCommitmentsAsync(Guid seatClassId, CancellationToken cancellationToken = default) =>
        context.SeatReservations.AsNoTracking().AnyAsync(r =>
            context.Seats.Any(s => s.Id == r.SeatId && s.SeatClassId == seatClassId && !s.IsDeleted)
            && (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Confirmed)
            && r.Trip!.Status != TripStatus.Arrived && r.Trip.Status != TripStatus.Completed
            && r.Trip.Status != TripStatus.Cancelled && r.Trip.Status != TripStatus.Terminated, cancellationToken);

    public void Add(SeatClass seatClass) => context.SeatClasses.Add(seatClass);
}
