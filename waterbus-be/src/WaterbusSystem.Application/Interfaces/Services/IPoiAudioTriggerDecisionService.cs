using WaterbusSystem.Application.Features.SightseeingAudio.Models;

namespace WaterbusSystem.Application.Interfaces.Services;

/// <summary>
/// Dịch vụ đánh giá và ra quyết định kích hoạt audio thuyết minh khi tàu vào vùng POI.
/// Đánh giá các điều kiện:
/// - Boat/Trip/Route khớp nhau
/// - Chuyến Sightseeing và đang EnRoute
/// - POI và RoutePoi active
/// - Tọa độ và TriggerRadiusM hợp lệ (không tự đặt bán kính mặc định khi null)
/// - AudioGuide tiếng Việt ("vi") đã publish và có URI hợp lệ
/// - Không bị chặn bởi Suspended/Terminated hoặc thông báo an toàn / Captain override
/// - Chống kích hoạt lặp lại trong cùng lượt ghé
/// </summary>
public interface IPoiAudioTriggerDecisionService
{
    /// <summary>
    /// Đánh giá vị trí tàu với danh sách ứng viên POI và thực hiện gửi lệnh trigger nếu thỏa mãn mọi điều kiện.
    /// </summary>
    /// <param name="position">Ảnh chụp vị trí tức thời của tàu từ GPS/tracking.</param>
    /// <param name="tripContext">Ngữ cảnh vận hành của chuyến tàu.</param>
    /// <param name="poiCandidates">Danh sách các POI ứng viên trên Route.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Kết quả đánh giá và quyết định kích hoạt.</returns>
    Task<PoiAudioTriggerEvaluationResult> EvaluateAndTriggerAsync(
        BoatPositionSnapshot position,
        TripSightseeingContext tripContext,
        IReadOnlyList<PoiGeofenceCandidate> poiCandidates,
        CancellationToken cancellationToken = default);
}
