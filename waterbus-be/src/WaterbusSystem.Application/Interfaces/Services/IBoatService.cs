using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.Boats;
using WaterbusSystem.Application.DTOs.Responses.Boats;

namespace WaterbusSystem.Application.Interfaces.Services;

public interface IBoatService
{
    Task<Guid> CreateAsync(CreateBoatRequest request, CancellationToken cancellationToken = default);

    Task UpdateAsync(Guid id, UpdateBoatRequest request, CancellationToken cancellationToken = default);

    Task<BoatResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PaginatedList<BoatResponse>> GetPageAsync(GetBoatsRequest request, CancellationToken cancellationToken = default);
}
