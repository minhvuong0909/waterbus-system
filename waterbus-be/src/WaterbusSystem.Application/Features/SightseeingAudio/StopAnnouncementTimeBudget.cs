namespace WaterbusSystem.Application.Features.SightseeingAudio;

/// <summary>Vi phạm ràng buộc của ngân sách thời gian, gắn với đúng trường để trả về cho Admin.</summary>
public sealed record StopAnnouncementBudgetViolation(string Property, string Message);

/// <summary>
/// Ngân sách thời gian của một chặng: các cửa sổ phát thanh và giới hạn bài thuyết minh.
/// Cửa sổ "rời bến" chứa thông báo rời bến + thuyết minh địa danh.
/// Cửa sổ "gần bến" chứa thông báo gần tới bến.
/// Thời gian đậu chứa thông báo cập bến.
/// </summary>
public sealed record StopAnnouncementTimeBudget(
    int SegmentCount,
    double SegmentMovingSeconds,
    double DepartingWindowSeconds,
    double NearWindowSeconds,
    double DwellSeconds,
    double MaxNarrationSeconds,
    int MaxNarrationCharacters,
    double MinDwellSeconds,
    double MinTripDurationSeconds,
    IReadOnlyList<StopAnnouncementBudgetViolation> Violations)
{
    public bool IsValid => Violations.Count == 0;
}

/// <summary>
/// Tính ngân sách thời gian từ cấu hình. Đây là nguồn sự thật duy nhất cho cả kiểm tra cấu hình
/// lẫn giới hạn độ dài bài thuyết minh, để không bao giờ xảy ra chồng tiếng trên tàu.
/// </summary>
public static class StopAnnouncementTimeBudgetCalculator
{
    public static StopAnnouncementTimeBudget Calculate(StopAnnouncementOptions options, int segmentCount)
    {
        var segments = Math.Max(1, segmentCount);
        var dwell = Math.Max(0, options.DwellTimeSeconds);
        var nearThreshold = Math.Clamp(options.NearThreshold, 0.1, 0.99);
        var gap = Math.Max(0, options.SafetyGapSeconds);

        var minDwell = options.ArrivedAnnouncementSeconds + gap;
        var requiredDepartingWindow = options.DepartingAnnouncementSeconds + options.MaxNarrationSeconds + gap;
        var requiredNearWindow = options.NearAnnouncementSeconds + gap;

        var minMoving = Math.Max(
            requiredDepartingWindow / nearThreshold,
            requiredNearWindow / (1 - nearThreshold));

        var movingBudget = options.TripDurationSeconds - (segments * dwell);
        var segmentMoving = movingBudget / segments;

        var departingWindow = segmentMoving * nearThreshold;
        var nearWindow = segmentMoving * (1 - nearThreshold);

        var narrationCapacity = departingWindow - options.DepartingAnnouncementSeconds - gap;
        var maxNarration = Math.Max(0, Math.Min(options.MaxNarrationSeconds, narrationCapacity));
        var maxCharacters = (int)Math.Floor(maxNarration * Math.Max(1, options.NarrationCharactersPerSecond));

        var minTripDuration = segments * (minMoving + dwell);

        var violations = new List<StopAnnouncementBudgetViolation>();

        if (dwell + 0.001 < minDwell)
        {
            violations.Add(new StopAnnouncementBudgetViolation(
                "DwellTimeSeconds",
                $"Thời gian dừng tại bến phải tối thiểu {minDwell:F0} giây để phát trọn thông báo cập bến."));
        }

        if (segmentMoving + 0.001 < minMoving)
        {
            violations.Add(new StopAnnouncementBudgetViolation(
                "TripDurationSeconds",
                $"Thời lượng toàn chuyến phải tối thiểu {minTripDuration:F0} giây cho {segments} chặng "
                + $"(mỗi chặng cần ít nhất {minMoving:F0} giây di chuyển để thông báo rời bến, thuyết minh địa danh "
                + "và thông báo gần bến không chồng lên nhau)."));
        }

        if (maxNarration <= 0)
        {
            violations.Add(new StopAnnouncementBudgetViolation(
                "TripDurationSeconds",
                "Chặng di chuyển quá ngắn nên không thể phát thuyết minh địa danh. Hãy tăng thời lượng toàn chuyến."));
        }

        return new StopAnnouncementTimeBudget(
            segments, segmentMoving, departingWindow, nearWindow, dwell,
            maxNarration, maxCharacters, minDwell, minTripDuration, violations);
    }
}
