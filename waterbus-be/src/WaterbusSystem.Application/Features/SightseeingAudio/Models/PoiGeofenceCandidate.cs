namespace WaterbusSystem.Application.Features.SightseeingAudio.Models;

/// <summary>
/// Ứng viên POI trên tuyến hành trình cần được đánh giá geofence.
/// Tương ứng với cặp RoutePoi và PointOfInterest trong cơ sở dữ liệu.
/// </summary>
public record PoiGeofenceCandidate
{
    public Guid PoiId { get; init; }
    public string PoiName { get; init; } = string.Empty;
    public Guid RouteId { get; init; }
    public decimal? Latitude { get; init; }
    public decimal? Longitude { get; init; }

    /// <summary>
    /// Bán kính kích hoạt (mét) theo RoutePoi.TriggerRadiusM.
    /// Nếu null: không tự đặt bán kính mặc định; giữ hành vi không trigger.
    /// </summary>
    public int? TriggerRadiusM { get; init; }

    public int DisplayOrder { get; init; }
    public bool IsPoiActive { get; init; } = true;
    public bool IsRoutePoiActive { get; init; } = true;

    /// <summary>
    /// Danh sách các AudioGuide thuộc POI này.
    /// </summary>
    public IReadOnlyList<AudioGuideCandidate> AudioGuides { get; init; } = Array.Empty<AudioGuideCandidate>();
}
