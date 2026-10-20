namespace WaterbusSystem.Application.Features.SightseeingAudio;

/// <summary>GPS geofence demo chỉ bật có chủ đích; chưa có Onboard Player/ACK production.</summary>
public sealed class SightseeingAudioOptions
{
    public const string SectionName = "SightseeingAudio";
    public bool EnableGpsTriggerEvaluation { get; set; }
}
