using System.Text.RegularExpressions;

namespace WaterbusSystem.Infrastructure.Services.AiBooking;

/// <summary>
/// Ràng buộc thời gian rõ ràng trong câu hỏi; model vẫn trích xuất các điều kiện còn lại.
/// Không để một lần gọi tool thiếu ngày biến "hôm nay" thành toàn bộ ngày tương lai.
/// </summary>
internal sealed record AiBookingQueryIntent(
    DateOnly? RequiredDate,
    TimeOnly? EarliestDepartureTime,
    TimeOnly? LatestDepartureTime,
    bool NearestDeparturesOnly,
    bool ExplicitArrivalOnly,
    bool ExplicitDepartureOnly)
{
    public static AiBookingQueryIntent Parse(string message, DateTimeOffset now, bool allTripsRequest)
    {
        var today = DateOnly.FromDateTime(now.ToOffset(TimeSpan.FromHours(7)).DateTime);
        DateOnly? date = null;
        if (HasWord(message, "ngày kia") || Contains(message, "day after tomorrow"))
            date = today.AddDays(2);
        else if (HasWord(message, "mai") || Contains(message, "tomorrow"))
            date = today.AddDays(1);
        else if (HasWord(message, "hôm nay") || HasWord(message, "nay")
                 || Contains(message, "today") || Contains(message, "tonight")
                 || Contains(message, "this morning") || Contains(message, "this afternoon")
                 || Contains(message, "this evening"))
            date = today;

        TimeOnly? earliest = null;
        TimeOnly? latest = null;
        if (HasWord(message, "chiều tối"))
            (earliest, latest) = (new(16, 0), new(22, 0));
        else if (HasWord(message, "buổi tối") || HasWord(message, "tối")
                 || Contains(message, "tonight") || Contains(message, "this evening"))
            (earliest, latest) = (new(18, 0), new(23, 59));
        else if (HasWord(message, "buổi chiều") || HasWord(message, "chiều")
                 || Contains(message, "this afternoon"))
            (earliest, latest) = (new(12, 0), new(17, 59));
        else if (HasWord(message, "buổi sáng") || HasWord(message, "sáng")
                 || Contains(message, "this morning"))
            (earliest, latest) = (new(5, 0), new(11, 59));

        var nearestOnly = !allTripsRequest && (
            HasWord(message, "sắp khởi hành") || HasWord(message, "sắp tới")
            || HasWord(message, "gần nhất") || Contains(message, "next departure")
            || Contains(message, "next trip") || Contains(message, "departing soon")
            || Contains(message, "upcoming departures") || Contains(message, "upcoming trips"));

        var hasArrivalDirection = HasWord(message, "điểm đến") || HasWord(message, "bến đến")
            || HasWord(message, "đến bến") || HasWord(message, "tới bến")
            || Regex.IsMatch(message, @"(?<!\p{L})(?:đến|tới)\s+(?!trước\b|lúc\b|giờ\b|ngày\b)\p{L}",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            || HasWord(message, "destination") || Contains(message, "arriving at")
            || Contains(message, "arrive at") || Contains(message, "trips to");
        var hasDepartureDirection = HasWord(message, "điểm đi") || HasWord(message, "bến đi")
            || HasWord(message, "xuất phát từ") || HasWord(message, "khởi hành từ")
            || Regex.IsMatch(message, @"(?<!\p{L})từ\s+(?!\d|giá\b|mức\b)\p{L}",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            || HasWord(message, "origin")
            || Contains(message, "departing from") || Contains(message, "trips from");

        return new(date, earliest, latest, nearestOnly,
            hasArrivalDirection && !hasDepartureDirection,
            hasDepartureDirection && !hasArrivalDirection);
    }

    private static bool Contains(string message, string phrase) =>
        message.Contains(phrase, StringComparison.OrdinalIgnoreCase);

    private static bool HasWord(string message, string phrase) =>
        Regex.IsMatch(message, $@"(?<!\p{{L}}){Regex.Escape(phrase)}(?!\p{{L}})",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
