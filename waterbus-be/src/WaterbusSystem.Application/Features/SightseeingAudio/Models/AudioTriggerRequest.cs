namespace WaterbusSystem.Application.Features.SightseeingAudio.Models;

/// <summary>
/// Yêu cầu / đề xuất kích hoạt phát audio gửi tới Onboard Player trên tàu.
/// LƯU Ý: Đây là yêu cầu phát, CHƯA phải xác nhận âm thanh đã phát ra loa (BR-EXP-03).
/// </summary>
public record AudioTriggerRequest
{
    public Guid RequestId { get; init; } = Guid.NewGuid();
    public Guid TripId { get; init; }
    public Guid BoatId { get; init; }
    public Guid RouteId { get; init; }
    public Guid PoiId { get; init; }
    public string PoiName { get; init; } = string.Empty;
    public Guid AudioGuideId { get; init; }
    public string AudioAssetUri { get; init; } = string.Empty;
    public int? DurationSeconds { get; init; }
    public double DistanceMeters { get; init; }
    public int TriggerRadiusMeters { get; init; }
    public DateTimeOffset TriggeredAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Định danh lượt ghé (visit) nếu có cho tuyến vòng.
    /// </summary>
    public string? VisitId { get; init; }
}
