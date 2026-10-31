using WaterbusSystem.Application.Common.Models;

namespace WaterbusSystem.Application.Interfaces.Repositories;

public interface IBoatRepository
{
    /// <summary>Tàu đang hoạt động (chưa xoá) hoặc ném NotFoundException.</summary>
    Task<BoatTrackingRef> GetActiveForTrackingAsync(Guid boatId, CancellationToken cancellationToken = default);

    /// <summary>Mọi tàu đang hoạt động, sắp theo mã tàu.</summary>
    Task<List<BoatSummary>> ListActiveSummariesAsync(CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid boatId, CancellationToken cancellationToken = default);
}
