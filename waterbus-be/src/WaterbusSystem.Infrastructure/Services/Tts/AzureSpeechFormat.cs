using Microsoft.CognitiveServices.Speech;

namespace WaterbusSystem.Infrastructure.Services.Tts;

internal static class AzureSpeechFormat
{
    public static SpeechSynthesisOutputFormat ParseMp3(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !Enum.TryParse(value, ignoreCase: true, out SpeechSynthesisOutputFormat format)
            || !Enum.IsDefined(format)
            || !format.ToString().EndsWith("Mp3", StringComparison.Ordinal))
        {
            throw new ArgumentException("AzureSpeech:OutputFormat phải là định dạng MP3 hợp lệ.", nameof(value));
        }

        return format;
    }
}
