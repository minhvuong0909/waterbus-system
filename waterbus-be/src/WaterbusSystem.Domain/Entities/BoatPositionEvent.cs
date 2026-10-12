using WaterbusSystem.Domain.Common;
namespace WaterbusSystem.Domain.Entities;
/// <summary>Sự kiện GPS từ tàu — dùng để trigger audio sightseeing và tracking.</summary>
public class BoatPositionEvent : BaseEntity
{
    public Guid BoatId { get; set; }
    public Boat? Boat { get; set; }
    public Guid? TripId { get; set; }
    public Trip? Trip { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public DateTimeOffset ObservedAt { get; set; }
    /// <summary>"GPS" hoặc "Simulated"</summary>
    public string Source { get; set; } = "GPS";
    public decimal? SpeedKnots { get; set; }
}
