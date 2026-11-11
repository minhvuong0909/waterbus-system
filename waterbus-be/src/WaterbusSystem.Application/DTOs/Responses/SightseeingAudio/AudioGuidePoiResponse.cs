namespace WaterbusSystem.Application.DTOs.Responses.SightseeingAudio;

public record AudioGuidePoiResponse(Guid PoiId, string Name, bool HasActiveRoutePoi, Guid? AudioGuideId, string? AudioAssetUri, bool IsPublished);
