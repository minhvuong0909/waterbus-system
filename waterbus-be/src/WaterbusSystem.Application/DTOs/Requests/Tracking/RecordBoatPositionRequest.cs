namespace WaterbusSystem.Application.DTOs.Requests.Tracking;

public record RecordBoatPositionRequest(
    Guid BoatId,
    decimal Latitude,
    decimal Longitude,
    decimal? SpeedKnots,
    DateTimeOffset ObservedAt,
    string Source,
    Guid? ReportedByCaptainId);
