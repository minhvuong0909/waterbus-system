namespace WaterbusSystem.Application.DTOs.Responses.SightseeingAudio;

public record AudioGuidePoisPageResponse(IReadOnlyList<AudioGuidePoiResponse> Items, int Page, int PageSize, int TotalCount);
