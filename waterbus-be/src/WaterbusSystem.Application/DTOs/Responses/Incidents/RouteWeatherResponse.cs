public static class RouteWeatherStatus
{
    public const string Normal = "Normal";
    public const string Caution = "Caution";
    public const string Danger = "Danger";
    public const string Unknown = "Unknown";
}

// Thời tiết hiện tại của một tuyến: quan sát tệ nhất trong các bến của tuyến. 
// Tốc độ gió tính bằng m/s; FE tự đổi sang km/h.
public record RouteWeatherResponse(
    Guid RouteId,
    string RouteCode,
    string RouteName,
    string Status,
    double? WindSpeedMs,
    double? WindGustMs,
    int? ConditionCode,
    string? Description,
    DateTimeOffset? ObservedAt,
    int PointsChecked,
    int PointsFailed
);