namespace WaterbusSystem.Application.DTOs.Requests.SightseeingAudio;

/// <summary>
/// Yêu cầu cập nhật cấu hình bộ mô phỏng thông báo bến (các trường null sẽ giữ nguyên cấu hình cũ).
/// </summary>
public sealed record UpdateStopAnnouncementRequest(
    double? SegmentDurationSeconds,
    double? DwellTimeSeconds,
    double? NearThreshold,
    bool? UseSimulatedSegmentDuration);
