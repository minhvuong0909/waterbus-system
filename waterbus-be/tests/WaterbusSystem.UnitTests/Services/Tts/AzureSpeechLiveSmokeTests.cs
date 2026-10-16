using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WaterbusSystem.Infrastructure.Services.Tts;
using Xunit;

namespace WaterbusSystem.UnitTests.Services.Tts;

/// <summary>
/// Chỉ gọi Azure khi WATERBUS_AZURE_SPEECH_SMOKE=1; không chạy trong CI mặc định.
/// Một test pass khi không opt-in KHÔNG chứng minh Azure thật đã hoạt động.
/// </summary>
public class AzureSpeechLiveSmokeTests
{
    [Fact]
    public async Task SynthesizeVietnameseMp3_WhenExplicitlyEnabled()
    {
        if (Environment.GetEnvironmentVariable("WATERBUS_AZURE_SPEECH_SMOKE") != "1")
            return;

        var options = new AzureSpeechOptions
        {
            SubscriptionKey = RequireEnvironment("AzureSpeech__SubscriptionKey"),
            Region = RequireEnvironment("AzureSpeech__Region"),
            DefaultVoiceName = RequireEnvironment("AzureSpeech__DefaultVoiceName"),
            OutputFormat = RequireEnvironment("AzureSpeech__OutputFormat"),
            TimeoutSeconds = 60
        };
        var service = new AzureSpeechTtsService(
            Options.Create(options),
            new DefaultAzureSpeechSynthesizerClient(),
            NullLogger<AzureSpeechTtsService>.Instance);

        var result = await service.SynthesizeSpeechAsync("Chào mừng quý khách đến với chuyến tham quan sông Sài Gòn.");

        result.ContentType.Should().Be("audio/mpeg");
        result.AudioData.Should().NotBeEmpty();
        result.ByteLength.Should().Be(result.AudioData.Length);
        result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
        // MP3 có thể bắt đầu bằng ID3 tag hoặc MPEG frame sync.
        var hasId3 = result.AudioData.Length >= 3
            && result.AudioData[0] == (byte)'I'
            && result.AudioData[1] == (byte)'D'
            && result.AudioData[2] == (byte)'3';
        var hasMpegFrame = result.AudioData.Length >= 2
            && result.AudioData[0] == 0xFF
            && (result.AudioData[1] & 0xE0) == 0xE0;
        (hasId3 || hasMpegFrame).Should().BeTrue("Azure phải trả bytes MP3 thật");
    }

    private static string RequireEnvironment(string name) =>
        Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException($"Thiếu biến môi trường {name} cho Azure smoke test.");
}
