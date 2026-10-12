using WaterbusSystem.Domain.Common;
namespace WaterbusSystem.Domain.Entities;
/// <summary>Điểm du lịch/di tích trên tuyến sightseeing</summary>
public class PointOfInterest : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<RoutePoi> RoutePois { get; set; } = new List<RoutePoi>();
    public ICollection<AudioGuide> AudioGuides { get; set; } = new List<AudioGuide>();
}
