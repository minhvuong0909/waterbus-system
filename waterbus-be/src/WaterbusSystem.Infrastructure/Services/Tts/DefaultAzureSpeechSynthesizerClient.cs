using Microsoft.CognitiveServices.Speech;

namespace WaterbusSystem.Infrastructure.Services.Tts;

/// <summary>
/// Client mặc định sử dụng Microsoft Cognitive Services Speech SDK để giao tiếp với Azure AI Speech.
/// </summary>
public class DefaultAzureSpeechSynthesizerClient : IAzureSpeechSynthesizerClient
{
    public async Task<SpeechSynthesisResultData> SynthesizeAsync(
        string text,
        string voiceName,
        string outputFormat,
        string subscriptionKey,
        string region,
        CancellationToken cancellationToken)
    {
        var speechConfig = SpeechConfig.FromSubscription(subscriptionKey, region);
        speechConfig.SpeechSynthesisVoiceName = voiceName;

        speechConfig.SetSpeechSynthesisOutputFormat(AzureSpeechFormat.ParseMp3(outputFormat));

        // null audio config -> tổng hợp lưu vào bộ nhớ thay vì xuất ra loa
        using var synthesizer = new SpeechSynthesizer(speechConfig, null);

        var speakTask = synthesizer.SpeakTextAsync(text);
        SpeechSynthesisResult result;
        try
        {
            result = await speakTask.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // WaitAsync chỉ dừng chờ; báo SDK dừng tổng hợp đang chạy.
            try
            {
                await synthesizer.StopSpeakingAsync().WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch (Exception)
            {
                // Best effort: vẫn giữ nguyên tín hiệu hủy của caller.
            }

            throw;
        }

        using (result)
        {

            if (result.Reason == ResultReason.SynthesizingAudioCompleted)
            {
                return new SpeechSynthesisResultData(
                    AudioData: result.AudioData ?? Array.Empty<byte>(),
                    Duration: result.AudioDuration,
                    Succeeded: true);
            }

            if (result.Reason == ResultReason.Canceled)
            {
                var cancellation = SpeechSynthesisCancellationDetails.FromResult(result);
                return new SpeechSynthesisResultData(
                    AudioData: Array.Empty<byte>(),
                    Duration: TimeSpan.Zero,
                    Succeeded: false,
                    CancellationReason: cancellation.Reason.ToString(),
                    ErrorCode: cancellation.ErrorCode.ToString());
            }

            return new SpeechSynthesisResultData(
                AudioData: Array.Empty<byte>(),
                Duration: TimeSpan.Zero,
                Succeeded: false,
                CancellationReason: result.Reason.ToString(),
                ErrorCode: "Unknown");
        }
    }
}
