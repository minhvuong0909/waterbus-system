using WaterbusSystem.Domain.Common;
namespace WaterbusSystem.Domain.Entities;
/// <summary>
/// File audio thuyết minh cho một POI.
/// Scope hiện tại: chỉ tiếng Việt ("vi").
/// Phát qua loa tàu — KHÔNG phải thiết bị cá nhân hành khách.
/// </summary>
public class AudioGuide : BaseEntity
{
    public Guid PoiId { get; set; }
    public PointOfInterest? Poi { get; set; }
    /// <summary>Mã ngôn ngữ — hiện chỉ "vi"</summary>
    public string LanguageCode { get; set; } = "vi";
    public string AudioAssetUri { get; set; } = string.Empty;
    public int? DurationSeconds { get; set; }
    public bool IsPublished { get; set; } = false;
}
