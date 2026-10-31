using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Models;
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
}
