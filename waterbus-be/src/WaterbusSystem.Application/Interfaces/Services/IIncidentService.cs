using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.Incidents;
using WaterbusSystem.Application.DTOs.Responses.Incidents;

namespace WaterbusSystem.Application.Interfaces.Services;

public interface IIncidentService
{
    Task<PaginatedList<IncidentSummaryResponse>> GetPageAsync(GetIncidentsRequest request, CancellationToken cancellationToken = default);

    Task<IncidentDetailResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Admin đóng sự cố đang mở. Không đổi Trip.Status (BR-INC-03).</summary>
    Task<ResolveIncidentResponse> ResolveAsync(Guid incidentId, CancellationToken cancellationToken = default);
}
