namespace WaterbusSystem.Application.DTOs.Responses.SightseeingAudio;

public record SynthesizeAudioResponse(string AudioUrl, string AssetKey, string ContentType, long ByteLength, int DurationSeconds, string VoiceName);
