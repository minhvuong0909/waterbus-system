using WaterbusSystem.Application.DTOs.Responses.SightseeingAudio;
using WaterbusSystem.Application.Features.SightseeingAudio.Models;
using WaterbusSystem.Domain.Entities;

namespace WaterbusSystem.Application.Interfaces.Repositories;

/// <summary>Truy cập dữ liệu POI/audio và snapshot hành trình cho geofence.</summary>
public interface ISightseeingAudioRepository
{
    Task<bool> ActivePoiExistsAsync(Guid poiId, CancellationToken cancellationToken);
    Task<AudioGuide?> FindGuideForUpdateAsync(Guid poiId, string languageCode, CancellationToken cancellationToken);
    void AddGuide(AudioGuide guide);
    Task<AudioGuidePoisPageResponse> GetPoisAsync(int pageNumber, int pageSize, CancellationToken cancellationToken);
    Task<SightseeingAudioSnapshot?> GetTrackingSnapshotAsync(Guid tripId, Guid boatId, CancellationToken cancellationToken);

    /// <summary>Chuyến kèm các bến theo thứ tự thực tế phục vụ (VisitOrder), dùng để ghép thông báo bến.</summary>
    Task<StopAnnouncementSnapshot?> GetStopAnnouncementSnapshotAsync(Guid tripId, CancellationToken cancellationToken);

    /// <summary>Địa danh trên tuyến (kèm bài thuyết minh tiếng Việt đã publish nếu có), sắp theo thứ tự xuất hiện.</summary>
    Task<IReadOnlyList<RouteNarrationCandidate>> ListRouteNarrationCandidatesAsync(
        Guid routeId, CancellationToken cancellationToken);

    /// <summary>
    /// Số chặng lớn nhất trong các tuyến đang hoạt động (0 khi chưa có tuyến).
    /// Dùng dựng ngân sách thời gian cho cấu hình thông báo bến.
    /// </summary>
    Task<int> GetMaxRouteSegmentCountAsync(CancellationToken cancellationToken);
}
