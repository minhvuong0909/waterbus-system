namespace WaterbusSystem.Application.DTOs.Responses.SightseeingAudio;

/// <summary>
/// Cấu hình hiện tại của bộ mô phỏng thông báo bến kèm ngân sách thời gian đã dựng.
/// </summary>
public sealed record StopAnnouncementConfigResponse(
    double TripDurationSeconds,
    double DwellTimeSeconds,
    double NearThreshold,
    bool UseSimulatedSegmentDuration,
    StopAnnouncementBudgetResponse Budget);
