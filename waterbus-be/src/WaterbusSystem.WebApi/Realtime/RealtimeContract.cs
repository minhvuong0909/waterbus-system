namespace WaterbusSystem.WebApi.Realtime;

/// <summary>
/// Đường dẫn hub, tên group và tên event SignalR dùng chung (hợp đồng với FE, xem AGENTS.md).
/// </summary>
public static class RealtimeContract
{
    public static class Paths
    {
        public const string Prefix = "/hubs";
        public const string BoatTracking = Prefix + "/boat-tracking";
        public const string SeatSync = Prefix + "/seat-sync";
        public const string OnboardAudio = Prefix + "/onboard-audio";
    }

    public static class Groups
    {
        public const string Fleet = "fleet";
        public static string Boat(Guid boatId) => $"boat:{boatId}";
        public static string Trip(Guid tripId) => $"trip:{tripId}";
        public static string OnboardAudio(Guid boatId) => $"onboard-audio:{boatId}";
    }

    public static class Events
    {
        public const string BoatPosition = "boatPosition";
        public const string SeatHeld = "seatHeld";
        public const string SeatReleased = "seatReleased";
        public const string SeatSold = "seatSold";
        public const string WeatherAlert = "weatherAlert";
        public const string TripStatusChanged = "tripStatusChanged";
        public const string IncidentRaised = "incidentRaised";
        public const string AudioTrigger = "audioTrigger";
        public const string AudioControl = "audioControl";
        public const string AudioState = "audioState";
    }
}
