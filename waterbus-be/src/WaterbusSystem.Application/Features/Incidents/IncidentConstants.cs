namespace WaterbusSystem.Application.Features.Incidents;

/// <summary>
/// Giá trị chuỗi của Incident (khớp mô tả trong entity Incident / IncidentTrip).
/// </summary>
public static class IncidentConstants
{
    public static class Sources
    {
        public const string Staff = "Staff";
        public const string Captain = "Captain";
        public const string System = "System";
        public const string WeatherService = "WeatherService";
    }

    public static class Types
    {
        public const string Technical = "Technical";
        public const string Safety = "Safety";
        public const string Weather = "Weather";
        public const string Other = "Other";
    }

    public static class Scopes
    {
        public const string Route = "Route";
        public const string Boat = "Boat";
        public const string Trip = "Trip";
        public const string MultipleTrips = "MultipleTrips";
    }

    public static class Statuses
    {
        public const string Open = "Open";
        public const string Resolved = "Resolved";
        public const string Closed = "Closed";
    }

    public static class Severities
    {
        public const string Medium = "Medium";
        public const string High = "High";

        /// <summary>Thứ hạng để so sánh mức độ (cao hơn = nghiêm trọng hơn).</summary>
        public static int Rank(string? severity) => severity switch
        {
            High => 2,
            Medium => 1,
            _ => 0
        };
    }
}
