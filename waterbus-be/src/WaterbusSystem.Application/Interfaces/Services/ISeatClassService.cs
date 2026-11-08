using WaterbusSystem.Application.DTOs.Requests.SeatInventory;
using WaterbusSystem.Application.DTOs.Responses.SeatInventory;

namespace WaterbusSystem.Application.Interfaces.Services;

public interface ISeatClassService
{
    Task<List<SeatClassResponse>> ListAsync(GetSeatClassesRequest request, CancellationToken cancellationToken = default);
    Task<SeatClassResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateSeatClassRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateSeatClassRequest request, CancellationToken cancellationToken = default);
    Task DeactivateAsync(Guid id, CancellationToken cancellationToken = default);
    Task ActivateAsync(Guid id, CancellationToken cancellationToken = default);
}
