using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.Captains;
using WaterbusSystem.Application.DTOs.Responses.Captains;
using WaterbusSystem.Domain.Entities;

namespace WaterbusSystem.Application.Interfaces.Services;

public interface ICaptainService
{
    Task<Guid> CreateAsync(CreateCaptainRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateCaptainRequest request, CancellationToken ct = default);
    Task ChangeStatusAsync(Guid id, ChangeCaptainStatusRequest request, CancellationToken ct = default);
    Task<CaptainResponse> GetAsync(Guid id, CancellationToken ct = default);
    Task<PaginatedList<CaptainResponse>> ListAsync(GetCaptainsRequest request, CancellationToken ct = default);
}
