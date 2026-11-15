namespace WaterbusSystem.Application.DTOs.Requests.SightseeingAudio;

/// <summary>
/// Yêu cầu cập nhật cấu hình thời gian chuyến và ngưỡng báo hiệu bến.
/// Các trường null giữ nguyên cấu hình hiện tại.
/// </summary>
public sealed record UpdateStopAnnouncementRequest(
    double? TripDurationSeconds,
    double? DwellTimeSeconds,
    double? NearThreshold,
    bool? UseSimulatedSegmentDuration);
