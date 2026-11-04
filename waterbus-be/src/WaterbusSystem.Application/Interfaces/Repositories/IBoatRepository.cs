using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.Boats;
using WaterbusSystem.Application.DTOs.Responses.Boats;
using WaterbusSystem.Domain.Entities;

namespace WaterbusSystem.Application.Interfaces.Repositories;

public interface IBoatRepository
{
    /// <summary>Tàu đang hoạt động (chưa xoá) hoặc ném NotFoundException.</summary>
    Task<BoatTrackingRef> GetActiveForTrackingAsync(Guid boatId, CancellationToken cancellationToken = default);

    /// <summary>Mọi tàu đang hoạt động, sắp theo mã tàu.</summary>
    Task<List<BoatSummary>> ListActiveSummariesAsync(CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid boatId, CancellationToken cancellationToken = default);

    Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>Tàu (có theo dõi để sửa) hoặc ném NotFoundException.</summary>
    Task<Boat> GetRequiredByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Chi tiết tàu (chỉ đọc) hoặc ném NotFoundException.</summary>
    Task<BoatResponse> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PaginatedList<BoatResponse>> GetPageAsync(GetBoatsRequest request, CancellationToken cancellationToken = default);

    void Add(Boat boat);
}
