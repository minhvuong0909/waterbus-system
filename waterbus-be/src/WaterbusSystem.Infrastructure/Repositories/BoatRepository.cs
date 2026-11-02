using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.Boats;
using WaterbusSystem.Application.DTOs.Responses.Boats;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure.Persistence;

namespace WaterbusSystem.Infrastructure.Repositories;

public sealed class BoatRepository(ApplicationDbContext context) : IBoatRepository
{
    public async Task<BoatTrackingRef> GetActiveForTrackingAsync(Guid boatId, CancellationToken cancellationToken = default) =>
        await context.Boats.AsNoTracking()
            .Where(b => b.Id == boatId && !b.IsDeleted && b.IsActive)
            .Select(b => new BoatTrackingRef(b.Id, b.CaptainUserId))
            .FirstOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException(nameof(Boat), boatId);

    public Task<List<BoatSummary>> ListActiveSummariesAsync(CancellationToken cancellationToken = default) =>
        context.Boats.AsNoTracking()
            .Where(b => !b.IsDeleted && b.IsActive)
            .OrderBy(b => b.Code)
            .Select(b => new BoatSummary(b.Id, b.Code, b.Name))
            .ToListAsync(cancellationToken);

    public Task<bool> ExistsAsync(Guid boatId, CancellationToken cancellationToken = default) =>
        context.Boats.AnyAsync(b => b.Id == boatId && !b.IsDeleted, cancellationToken);

    public Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        context.Boats.AnyAsync(b => b.Code == code && !b.IsDeleted, cancellationToken);

    public async Task<Boat> GetRequiredByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await context.Boats.FirstOrDefaultAsync(b => b.Id == id && !b.IsDeleted, cancellationToken)
        ?? throw new NotFoundException(nameof(Boat), id);

    public async Task<BoatResponse> GetDetailAsync(Guid id, CancellationToken cancellationToken = default) =>
        await context.Boats.AsNoTracking()
            .Where(b => b.Id == id && !b.IsDeleted)
            .Select(b => new BoatResponse(
                b.Id, b.Code, b.Name, b.RegistrationNumber, b.TotalSeats, b.Manufacturer, b.YearBuilt, b.IsActive, b.CaptainUserId))
            .FirstOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException(nameof(Boat), id);

    public Task<PaginatedList<BoatResponse>> GetPageAsync(GetBoatsRequest request, CancellationToken cancellationToken = default)
    {
        var query = context.Boats.AsNoTracking().Where(b => !b.IsDeleted);

        if (request.IsActiveOnly.HasValue)
        {
            query = query.Where(b => b.IsActive == request.IsActiveOnly.Value);
        }

        var source = query
            .OrderBy(b => b.Code)
            .Select(b => new BoatResponse(
                b.Id, b.Code, b.Name, b.RegistrationNumber, b.TotalSeats, b.Manufacturer, b.YearBuilt, b.IsActive, b.CaptainUserId));

        return PaginatedList<BoatResponse>.CreateAsync(source, request.PageNumber, request.PageSize, cancellationToken);
    }

    public void Add(Boat boat) => context.Boats.Add(boat);
}
