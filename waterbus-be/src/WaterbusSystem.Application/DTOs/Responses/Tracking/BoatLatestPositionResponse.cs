namespace WaterbusSystem.Application.DTOs.Responses.Tracking;

/// <summary>Vị trí mới nhất của một tàu đang hoạt động.</summary>
public record BoatLatestPositionResponse(
    Guid BoatId,
    string BoatCode,
    string BoatName,
    Guid? TripId,
    decimal Latitude,
    decimal Longitude,
    decimal? SpeedKnots,
    DateTimeOffset ObservedAt);
