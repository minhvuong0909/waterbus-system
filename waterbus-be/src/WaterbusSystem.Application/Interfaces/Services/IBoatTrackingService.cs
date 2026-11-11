using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.Tracking;
using WaterbusSystem.Application.DTOs.Responses.Tracking;

namespace WaterbusSystem.Application.Interfaces.Services;

public interface IBoatTrackingService
{
    /// <summary>Ghi nhận một vị trí, cập nhật cache và phát realtime (GPS của Captain hoặc giả lập).</summary>
    Task<RecordBoatPositionResponse> RecordPositionAsync(RecordBoatPositionRequest request, CancellationToken cancellationToken = default);

    /// <summary>Snapshot vị trí mới nhất của các tàu đang hoạt động (radar Admin).</summary>
    Task<List<BoatLatestPositionResponse>> GetLatestPositionsAsync(CancellationToken cancellationToken = default);

    Task<PaginatedList<BoatPositionHistoryResponse>> GetPositionHistoryAsync(
        GetBoatPositionHistoryRequest request, CancellationToken cancellationToken = default);
}
