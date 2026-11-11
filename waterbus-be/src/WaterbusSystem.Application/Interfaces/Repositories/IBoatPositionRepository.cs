using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.Tracking;
using WaterbusSystem.Application.DTOs.Responses.Tracking;
using WaterbusSystem.Domain.Entities;

namespace WaterbusSystem.Application.Interfaces.Repositories;

public interface IBoatPositionRepository
{
    /// <summary>Thời điểm của bản ghi vị trí đã lưu mới nhất của tàu (null nếu chưa có).</summary>
    Task<DateTimeOffset?> GetLastPersistedAtAsync(Guid boatId, CancellationToken cancellationToken = default);

    /// <summary>Bản ghi vị trí đã lưu mới nhất của tàu (null nếu chưa có).</summary>
    Task<BoatPositionEvent?> GetLatestAsync(Guid boatId, CancellationToken cancellationToken = default);

    void Add(BoatPositionEvent positionEvent);

    Task<PaginatedList<BoatPositionHistoryResponse>> GetHistoryPageAsync(
        GetBoatPositionHistoryRequest request, CancellationToken cancellationToken = default);
}
