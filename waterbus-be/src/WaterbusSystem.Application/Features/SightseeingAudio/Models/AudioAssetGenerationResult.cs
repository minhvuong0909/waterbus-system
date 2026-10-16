namespace WaterbusSystem.Application.Features.SightseeingAudio.Models;

/// <summary>Metadata để owner POI liên kết với AudioGuide tiếng Việt sau khi duyệt.</summary>
public sealed record AudioAssetGenerationResult(
    Guid PoiId,
    string AssetKey,
    string DurableAssetUri,
    string ContentType,
    long ByteLength,
    int DurationSeconds,
    string VoiceName);
