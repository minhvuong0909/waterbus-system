using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Responses.Fares;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Application.Interfaces.Services;

namespace WaterbusSystem.Application.Features.Fares.Services;

public sealed class FareService(IFareRuleRepository fareRules, IDateTimeService dateTime) : IFareService
{
    public async Task<List<FareResponse>> GetActiveFaresAsync(CancellationToken cancellationToken = default)
    {
        var fares = await fareRules.ListActiveAsync(dateTime.Now, cancellationToken);

        // Sắp xếp phía client: InMemory/SQL đều cho cùng kết quả, giá decimal sắp đúng.
        return fares.OrderBy(f => f.TripType).ThenBy(f => f.Price).ToList();
    }
}
