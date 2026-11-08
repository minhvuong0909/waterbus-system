using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.Fares;
using WaterbusSystem.Application.DTOs.Responses.Fares;

namespace WaterbusSystem.Application.Interfaces.Services;

public interface IFareAdminService
{
    Task<PaginatedList<FareRuleAdminResponse>> GetPageAsync(GetFareRulesRequest request, CancellationToken cancellationToken = default);
    Task<FareRuleAdminResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateFareRuleRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateFareRuleRequest request, CancellationToken cancellationToken = default);
    Task DeactivateAsync(Guid id, CancellationToken cancellationToken = default);
}
