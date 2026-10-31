namespace WaterbusSystem.Application.DTOs.Requests.SightseeingAudio;

public record PublishAudioGuideRequest(Guid PoiId, string AudioAssetUri, int? DurationSeconds);
