using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.DTOs.Responses.Fares;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Infrastructure.Persistence;

namespace WaterbusSystem.Infrastructure.Repositories;

public sealed class FareRuleRepository(ApplicationDbContext context) : IFareRuleRepository
{
    public Task<List<FareResponse>> ListActiveAsync(DateTimeOffset now, CancellationToken cancellationToken = default) =>
        context.FareRules
            .AsNoTracking()
            .Where(f => !f.IsDeleted && f.IsActive
                        && f.EffectiveFrom <= now && (f.EffectiveTo == null || f.EffectiveTo > now)
                        && f.SeatClass != null && f.SeatClass.IsActive && !f.SeatClass.IsDeleted)
            .Select(f => new FareResponse(f.TripType, f.SeatClassId, f.SeatClass!.Code, f.SeatClass.Name, f.Price, f.Currency))
            .ToListAsync(cancellationToken);
}
