namespace WaterbusSystem.Application.DTOs.Requests.SightseeingAudio;

public record SynthesizeAudioRequest
{
    public string Text { get; init; } = string.Empty;
    public string? VoiceName { get; init; }

    public SynthesizeAudioRequest() { }

    public SynthesizeAudioRequest(string text, string? voiceName = null)
    {
        Text = text;
        VoiceName = voiceName;
    }
}
