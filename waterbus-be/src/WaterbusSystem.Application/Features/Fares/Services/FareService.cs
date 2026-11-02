using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Responses.Fares;
using WaterbusSystem.Application.Interfaces.Services;

namespace WaterbusSystem.Application.Features.Fares.Services;

public sealed class FareService(IApplicationDbContext context, IDateTimeService dateTime) : IFareService
{
    public async Task<List<FareResponse>> GetActiveFaresAsync(CancellationToken cancellationToken = default)
    {
        var now = dateTime.Now;
        var fares = await context.FareRules
            .AsNoTracking()
            .Where(f => !f.IsDeleted && f.IsActive
                        && f.EffectiveFrom <= now && (f.EffectiveTo == null || f.EffectiveTo > now)
                        && f.SeatClass != null && f.SeatClass.IsActive && !f.SeatClass.IsDeleted)
            .Select(f => new FareResponse(f.TripType, f.SeatClassId, f.SeatClass!.Code, f.SeatClass.Name, f.Price, f.Currency))
            .ToListAsync(cancellationToken);

        // Sắp xếp phía client: InMemory/SQL đều cho cùng kết quả, giá decimal sắp đúng.
        return fares.OrderBy(f => f.TripType).ThenBy(f => f.Price).ToList();
    }
}
