namespace WaterbusSystem.Application.DTOs.Responses.SightseeingAudio;

public record PublishedAudioGuideResponse(Guid AudioGuideId, Guid PoiId, string LanguageCode, string AudioAssetUri, int? DurationSeconds, bool IsPublished);
