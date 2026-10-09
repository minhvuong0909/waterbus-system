using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Application.Features.SightseeingAudio.Models;

/// <summary>
/// Ngữ cảnh vận hành của chuyến tàu đang được đánh giá geofence audio.
/// </summary>
public record TripSightseeingContext
{
    public Guid TripId { get; init; }
    public Guid BoatId { get; init; }
    public Guid RouteId { get; init; }
    public TripType TripType { get; init; } = TripType.Sightseeing;
    public TripStatus Status { get; init; } = TripStatus.EnRoute;

    /// <summary>
    /// Đang có thông báo an toàn hoặc Captain override khẩn cấp hay không (BR-EXP-06).
    /// </summary>
    public bool IsSafetyAnnouncementActive { get; init; }

    /// <summary>
    /// Nội dung thông báo an toàn đang kích hoạt (nếu có).
    /// </summary>
    public string? ActiveSafetyNotice { get; init; }

    /// <summary>
    /// Định danh lượt ghé tùy chọn (nếu hành trình vòng có nhiều lượt ghé cùng POI).
    /// </summary>
    public string? VisitId { get; init; }
}
