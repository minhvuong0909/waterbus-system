namespace WaterbusSystem.Application.Features.SightseeingAudio.Models;

/// <summary>
/// Dữ liệu vị trí tức thời của tàu (Boat) được cung cấp từ tầng tracking/GPS.
/// </summary>
public record BoatPositionSnapshot
{
    public Guid BoatId { get; init; }
    public Guid? TripId { get; init; }
    public decimal Latitude { get; init; }
    public decimal Longitude { get; init; }
    public DateTimeOffset ObservedAt { get; init; }
    public string Source { get; init; } = "GPS";
    public decimal? SpeedKnots { get; init; }
}
