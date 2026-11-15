using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.Incidents;
using WaterbusSystem.Application.DTOs.Responses.Incidents;
using WaterbusSystem.Domain.Entities;

namespace WaterbusSystem.Application.Interfaces.Repositories;

public interface IIncidentRepository
{
    /// <summary>Lấy Incident (có theo dõi để sửa) hoặc ném NotFoundException.</summary>
    Task<Incident> GetRequiredByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Lấy Incident (có theo dõi để sửa) kèm AffectedTrips hoặc ném NotFoundException.</summary>
    Task<Incident> GetRequiredWithAffectedTripsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Chi tiết Incident kèm chuyến bị ảnh hưởng (chỉ đọc) hoặc ném NotFoundException.</summary>
    Task<IncidentDetailResponse> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PaginatedList<IncidentSummaryResponse>> GetPageAsync(GetIncidentsRequest request, CancellationToken cancellationToken = default);

    void Add(Incident incident);

    void AddTripLink(IncidentTrip link);
}
