namespace WaterbusSystem.Application.DTOs.Responses.Tracking;

/// <summary>Một điểm trong lịch sử vị trí đã lưu.</summary>
public record BoatPositionHistoryResponse(
    Guid Id,
    Guid? TripId,
    decimal Latitude,
    decimal Longitude,
    decimal? SpeedKnots,
    DateTimeOffset ObservedAt,
    string Source);
