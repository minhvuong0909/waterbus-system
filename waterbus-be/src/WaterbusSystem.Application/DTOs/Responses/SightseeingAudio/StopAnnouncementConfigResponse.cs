namespace WaterbusSystem.Application.DTOs.Responses.SightseeingAudio;

/// <summary>
/// Cấu hình hiện tại của bộ mô phỏng thông báo bến.
/// </summary>
public sealed record StopAnnouncementConfigResponse(
    double SegmentDurationSeconds,
    double DwellTimeSeconds,
    double NearThreshold,
    bool UseSimulatedSegmentDuration);
