using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Infrastructure.Services.Tts;
using Xunit;

namespace WaterbusSystem.UnitTests.Services.Tts;

/// <summary>
/// Unit tests cho AzureSpeechTtsService và FakeTextToSpeechService.
/// Sử dụng Mock IAzureSpeechSynthesizerClient để kiểm thử đầy đủ các nhánh lỗi,
/// timeout, cancellation và mapping kết quả mà không cần Azure credential hay kết nối mạng.
/// </summary>
public class AzureSpeechTtsServiceTests
{
    private readonly Mock<IAzureSpeechSynthesizerClient> _clientMock = new();
    private readonly Mock<ILogger<AzureSpeechTtsService>> _loggerMock = new();

    private readonly AzureSpeechOptions _validOptions = new()
    {
        SubscriptionKey = "mock-secret-key-12345",
        Region = "southeastasia",
        DefaultVoiceName = "vi-VN-HoaiMyNeural",
        OutputFormat = "Audio24Khz48KBitRateMonoMp3",
        TimeoutSeconds = 10
    };

    private AzureSpeechTtsService CreateService(AzureSpeechOptions? options = null)
    {
        var optionsMock = Options.Create(options ?? _validOptions);
        return new AzureSpeechTtsService(optionsMock, _clientMock.Object, _loggerMock.Object);
    }

    // ── 1. Validate Input ────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public async Task SynthesizeSpeechAsync_NullOrWhitespaceText_ThrowsArgumentException(string? invalidText)
    {
        var service = CreateService();

        var act = () => service.SynthesizeSpeechAsync(invalidText!);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("text");
    }

    // ── 2. Validate Configuration ───────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SynthesizeSpeechAsync_MissingSubscriptionKey_ThrowsInvalidOperationException(string? key)
    {
        var options = new AzureSpeechOptions
        {
            SubscriptionKey = key,
            Region = "southeastasia"
        };
        var service = CreateService(options);

        var act = () => service.SynthesizeSpeechAsync("Chào mừng quý khách đến với Saigon Waterbus.");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*AzureSpeech:SubscriptionKey*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SynthesizeSpeechAsync_MissingRegion_ThrowsInvalidOperationException(string? region)
    {
        var options = new AzureSpeechOptions
        {
            SubscriptionKey = "valid-key",
            Region = region
        };
        var service = CreateService(options);

        var act = () => service.SynthesizeSpeechAsync("Chào mừng quý khách.");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*AzureSpeech:Region*");
    }

    [Fact]
    public async Task SynthesizeSpeechAsync_MissingVoice_ThrowsInvalidOperationException()
    {
        var service = CreateService(new AzureSpeechOptions
        {
            SubscriptionKey = "mock-key", Region = "southeastasia",
            OutputFormat = "Audio24Khz48KBitRateMonoMp3"
        });

        var act = () => service.SynthesizeSpeechAsync("Chào quý khách.");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*AzureSpeech:DefaultVoiceName*");
    }

    [Fact]
    public async Task SynthesizeSpeechAsync_MissingOutputFormat_ThrowsInvalidOperationException()
    {
        var service = CreateService(new AzureSpeechOptions
        {
            SubscriptionKey = "mock-key", Region = "southeastasia",
            DefaultVoiceName = "vi-VN-HoaiMyNeural"
        });

        var act = () => service.SynthesizeSpeechAsync("Chào quý khách.");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*AzureSpeech:OutputFormat*");
    }

    [Theory]
    [InlineData("Riff24Khz16BitMonoPcm")]
    [InlineData("not-a-format")]
    public async Task SynthesizeSpeechAsync_NonMp3Format_RejectsBeforeCallingProvider(string format)
    {
        var service = CreateService();

        var act = () => service.SynthesizeSpeechAsync("Chào quý khách.",
            new TextToSpeechOptions { OutputFormat = format });

        await act.Should().ThrowAsync<ArgumentException>();
        _clientMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SynthesizeSpeechAsync_NonVietnameseVoice_RejectsBeforeCallingProvider()
    {
        var service = CreateService();

        var act = () => service.SynthesizeSpeechAsync("Chào quý khách.",
            new TextToSpeechOptions { VoiceName = "en-US-AvaNeural" });

        await act.Should().ThrowAsync<ArgumentException>();
        _clientMock.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task SynthesizeSpeechAsync_InvalidConfiguredTimeout_Throws(int seconds)
    {
        var config = new AzureSpeechOptions
        {
            SubscriptionKey = "mock-key", Region = "southeastasia",
            DefaultVoiceName = "vi-VN-HoaiMyNeural",
            OutputFormat = "Audio24Khz48KBitRateMonoMp3",
            TimeoutSeconds = seconds
        };
        var service = CreateService(config);

        var act = () => service.SynthesizeSpeechAsync("Chào quý khách.");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*TimeoutSeconds*");
    }

    [Fact]
    public async Task SynthesizeSpeechAsync_NonPositiveOverrideTimeout_Throws()
    {
        var service = CreateService();

        var act = () => service.SynthesizeSpeechAsync("Chào quý khách.",
            new TextToSpeechOptions { Timeout = TimeSpan.Zero });

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
        _clientMock.VerifyNoOtherCalls();
    }

    // ── 3. Success Cases ────────────────────────────────────────────────────

    [Fact]
    public async Task SynthesizeSpeechAsync_ValidInput_ReturnsAudioSynthesisResultWithMetadata()
    {
        var sampleBytes = new byte[] { 0xFF, 0xFB, 0x90, 0x64, 0x12, 0x34 };
        var sampleDuration = TimeSpan.FromSeconds(8.5);

        _clientMock.Setup(c => c.SynthesizeAsync(
                "Tàu đang tiến vào bến Bạch Đằng.",
                "vi-VN-HoaiMyNeural",
                "Audio24Khz48KBitRateMonoMp3",
                "mock-secret-key-12345",
                "southeastasia",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpeechSynthesisResultData(
                AudioData: sampleBytes,
                Duration: sampleDuration,
                Succeeded: true));

        var service = CreateService();

        var result = await service.SynthesizeSpeechAsync("Tàu đang tiến vào bến Bạch Đằng.");

        result.Should().NotBeNull();
        result.AudioData.Should().BeEquivalentTo(sampleBytes);
        result.ContentType.Should().Be("audio/mpeg");
        result.Duration.Should().Be(sampleDuration);
        result.VoiceName.Should().Be("vi-VN-HoaiMyNeural");
        result.ByteLength.Should().Be(sampleBytes.Length);
    }

    [Fact]
    public async Task SynthesizeSpeechAsync_CustomOptions_OverridesVoiceAndOutputFormat()
    {
        var sampleBytes = new byte[] { 0x01, 0x02, 0x03 };
        var customOptions = new TextToSpeechOptions
        {
            VoiceName = "vi-VN-NamMinhNeural",
            OutputFormat = "Audio16Khz32KBitRateMonoMp3",
            Timeout = TimeSpan.FromSeconds(5)
        };

        _clientMock.Setup(c => c.SynthesizeAsync(
                "Thông báo an toàn.",
                "vi-VN-NamMinhNeural",
                "Audio16Khz32KBitRateMonoMp3",
                "mock-secret-key-12345",
                "southeastasia",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpeechSynthesisResultData(
                AudioData: sampleBytes,
                Duration: TimeSpan.FromSeconds(3),
                Succeeded: true));

        var service = CreateService();

        var result = await service.SynthesizeSpeechAsync("Thông báo an toàn.", customOptions);

        result.VoiceName.Should().Be("vi-VN-NamMinhNeural");
        _clientMock.Verify(c => c.SynthesizeAsync(
            "Thông báo an toàn.",
            "vi-VN-NamMinhNeural",
            "Audio16Khz32KBitRateMonoMp3",
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── 4. Cancellation & Timeout ───────────────────────────────────────────

    [Fact]
    public async Task SynthesizeSpeechAsync_CallerCancelledToken_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Hủy ngay từ đầu

        _clientMock.Setup(c => c.SynthesizeAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var service = CreateService();

        var act = () => service.SynthesizeSpeechAsync("Kiểm tra hủy tác vụ.", cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _clientMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SynthesizeSpeechAsync_TimeoutOccurs_ThrowsTextToSpeechException()
    {
        var shortTimeoutOptions = new AzureSpeechOptions
        {
            SubscriptionKey = "mock-key",
            Region = "southeastasia",
            DefaultVoiceName = "vi-VN-HoaiMyNeural",
            OutputFormat = "Audio24Khz48KBitRateMonoMp3",
            TimeoutSeconds = 1 // 1 giây timeout
        };

        // Giả lập client bị hủy do timeout từ linked token
        _clientMock.Setup(c => c.SynthesizeAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, string, string, string, string, CancellationToken>(async (_, _, _, _, _, ct) =>
            {
                await Task.Delay(2000, ct); // delay lâu hơn timeout
                return new SpeechSynthesisResultData(Array.Empty<byte>(), TimeSpan.Zero, true);
            });

        var service = CreateService(shortTimeoutOptions);

        var act = () => service.SynthesizeSpeechAsync("Đoạn văn bản dài quá hạn.");

        await act.Should().ThrowAsync<TextToSpeechException>()
            .WithMessage("*thời gian chờ*");
    }

    // ── 5. Provider Errors ──────────────────────────────────────────────────

    [Fact]
    public async Task SynthesizeSpeechAsync_ProviderReturnsFailed_DoesNotExposeProviderDetails()
    {
        _clientMock.Setup(c => c.SynthesizeAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpeechSynthesisResultData(
                AudioData: Array.Empty<byte>(),
                Duration: TimeSpan.Zero,
                Succeeded: false,
                CancellationReason: "Error",
                ErrorCode: "AuthenticationFailure"));

        var service = CreateService();

        var act = () => service.SynthesizeSpeechAsync("Kiểm tra lỗi xác thực.");

        var ex = await act.Should().ThrowAsync<TextToSpeechException>();
        ex.Which.ErrorCode.Should().Be("AuthenticationFailure");
        ex.Which.Message.Should().NotContain("Subscription key is invalid");
        ex.Which.Message.Should().NotContain(_validOptions.SubscriptionKey);
    }

    [Fact]
    public async Task SynthesizeSpeechAsync_UnsafeProviderCode_DoesNotExposeSecret()
    {
        _clientMock.Setup(c => c.SynthesizeAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpeechSynthesisResultData(
                Array.Empty<byte>(), TimeSpan.Zero, false,
                ErrorCode: "mock-secret-key-12345"));
        var service = CreateService();

        var act = () => service.SynthesizeSpeechAsync("Nội dung hợp lệ.");

        var ex = await act.Should().ThrowAsync<TextToSpeechException>();
        ex.Which.ErrorCode.Should().Be("ProviderError");
        ex.Which.Message.Should().NotContain(_validOptions.SubscriptionKey);
    }

    [Fact]
    public async Task SynthesizeSpeechAsync_ProviderReturnsEmptyAudio_ThrowsTextToSpeechException()
    {
        _clientMock.Setup(c => c.SynthesizeAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpeechSynthesisResultData(
                AudioData: Array.Empty<byte>(),
                Duration: TimeSpan.Zero,
                Succeeded: true));

        var service = CreateService();

        var act = () => service.SynthesizeSpeechAsync("Nội dung hợp lệ.");

        await act.Should().ThrowAsync<TextToSpeechException>()
            .WithMessage("*dữ liệu âm thanh rỗng*");
    }

    [Fact]
    public async Task SynthesizeSpeechAsync_ClientThrowsUnexpectedException_DoesNotExposeRawException()
    {
        _clientMock.Setup(c => c.SynthesizeAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("provider-error-mock-secret-key-12345"));

        var service = CreateService();

        var act = () => service.SynthesizeSpeechAsync("Nội dung.");

        var ex = await act.Should().ThrowAsync<TextToSpeechException>();
        ex.Which.InnerException.Should().BeNull();
        ex.Which.Message.Should().NotContain(_validOptions.SubscriptionKey);
    }

    // ── 6. FakeTextToSpeechService Tests ────────────────────────────────────

    [Fact]
    public async Task FakeTextToSpeechService_ValidText_ReturnsTestBytesAndDuration()
    {
        var fakeService = new FakeTextToSpeechService();

        var result = await fakeService.SynthesizeSpeechAsync("Chào mừng quý khách đến với tour du lịch sông Sài Gòn.");

        result.Should().NotBeNull();
        result.AudioData.Should().NotBeEmpty();
        result.ContentType.Should().Be("audio/mpeg");
        result.Duration.TotalSeconds.Should().BeGreaterThan(0);
        result.VoiceName.Should().Be("vi-VN-HoaiMyNeural");
    }

    [Fact]
    public async Task FakeTextToSpeechService_EmptyText_ThrowsArgumentException()
    {
        var fakeService = new FakeTextToSpeechService();

        var act = () => fakeService.SynthesizeSpeechAsync("   ");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task FakeTextToSpeechService_CancelledToken_ThrowsOperationCanceledException()
    {
        var fakeService = new FakeTextToSpeechService();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => fakeService.SynthesizeSpeechAsync("Văn bản bất kỳ", cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
