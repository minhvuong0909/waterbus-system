namespace WaterbusSystem.Application.Features.SightseeingAudio.Models;

/// <summary>
/// Ứng viên bản thuyết minh audio cho một POI.
/// Scope hiện tại: chỉ tiếng Việt ("vi") và phát qua loa tàu.
/// </summary>
public record AudioGuideCandidate
{
    public Guid AudioGuideId { get; init; }
    public Guid PoiId { get; init; }
    public string LanguageCode { get; init; } = "vi";
    public string AudioAssetUri { get; init; } = string.Empty;
    public int? DurationSeconds { get; init; }
    public bool IsPublished { get; init; }
}
