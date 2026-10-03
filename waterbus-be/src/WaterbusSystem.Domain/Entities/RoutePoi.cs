using WaterbusSystem.Domain.Common;
namespace WaterbusSystem.Domain.Entities;
/// <summary>Liên kết POI với Route sightseeing và thứ tự xuất hiện</summary>
public class RoutePoi : BaseEntity
{
    public Guid RouteId { get; set; }
    public Route? Route { get; set; }
    public Guid PoiId { get; set; }
    public PointOfInterest? Poi { get; set; }
    public int DisplayOrder { get; set; }
    /// <summary>Bán kính (mét) kể từ vị trí tàu để trigger phát audio</summary>
    public int? TriggerRadiusM { get; set; }
    public bool IsActive { get; set; } = true;
}
