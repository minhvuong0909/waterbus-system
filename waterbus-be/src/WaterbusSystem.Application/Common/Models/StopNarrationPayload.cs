namespace WaterbusSystem.Application.Common.Models;

/// <summary>Bài thuyết minh đi kèm thông báo rời bến của chuyến Sightseeing.</summary>
public sealed record StopNarrationPayload(
    Guid PoiId,
    string PoiName,
    string Text,
    string? AssetKey,
    int? DurationSeconds);
