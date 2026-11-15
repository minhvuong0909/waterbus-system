namespace WaterbusSystem.Application.DTOs.Responses.SightseeingAudio;

/// <summary>Vi phạm ràng buộc ngân sách thời gian, gắn với trường cấu hình tương ứng.</summary>
public sealed record StopAnnouncementBudgetViolationResponse(string Property, string Message);

/// <summary>
/// Ngân sách thời gian của một chặng, dựng từ cấu hình hiện tại.
/// FE dùng để hiển thị cảnh báo và giới hạn độ dài kịch bản thuyết minh.
/// </summary>
public sealed record StopAnnouncementBudgetResponse(
    int SegmentCount,
    double SegmentMovingSeconds,
    double DepartingWindowSeconds,
    double NearWindowSeconds,
    double DwellSeconds,
    double MaxNarrationSeconds,
    int MaxNarrationCharacters,
    double MinDwellSeconds,
    double MinTripDurationSeconds,
    bool IsValid,
    IReadOnlyList<StopAnnouncementBudgetViolationResponse> Violations);
