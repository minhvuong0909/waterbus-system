using WaterbusSystem.Application.DTOs.Requests.SightseeingAudio;
using FluentAssertions;
using Moq;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.Features.SightseeingAudio.Models;
using WaterbusSystem.Application.Services.SightseeingAudio;
using Xunit;

namespace WaterbusSystem.UnitTests.SightseeingAudio;

public class AudioAssetGenerationServiceTests
{
    private readonly Mock<ITextToSpeechService> _tts = new();
    private readonly Mock<IAudioStorageService> _storage = new();

    private static GenerateAudioAssetRequest Request() => new(
        Guid.NewGuid(),
        "Đây là kịch bản tiếng Việt đã được duyệt.",
        "pois/poi-1/vi/v1.mp3");

    [Fact]
    public async Task GenerateAsync_SynthesizesThenStoresMp3AndReturnsMetadata()
    {
        var request = Request();
        var audio = new AudioSynthesisResult([0x49, 0x44, 0x33, 0x01], "audio/mpeg",
            TimeSpan.FromSeconds(12.4), "vi-VN-HoaiMyNeural", 4);
        StoreAudioRequest? storedCommand = null;
        _tts.Setup(x => x.SynthesizeSpeechAsync(request.ApprovedScriptText, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(audio);
        _storage.Setup(x => x.StoreAudioAsync(It.IsAny<StoreAudioRequest>(), It.IsAny<CancellationToken>()))
            .Callback<StoreAudioRequest, CancellationToken>((command, _) => storedCommand = command)
            .ReturnsAsync(new AudioStorageResult("https://storage.example.com/audio-guides-private/pois/poi-1/vi/v1.mp3",
                "audio/mpeg", 4, DateTimeOffset.UtcNow));

        var result = await new AudioAssetGenerationService(_tts.Object, _storage.Object).GenerateAsync(request);

        result.PoiId.Should().Be(request.PoiId);
        result.AssetKey.Should().Be(request.AssetKey);
        result.DurationSeconds.Should().Be(13);
        result.VoiceName.Should().Be("vi-VN-HoaiMyNeural");
        storedCommand.Should().NotBeNull();
        storedCommand!.PoiId.Should().Be(request.PoiId);
        storedCommand.LanguageCode.Should().Be("vi");
        storedCommand.AssetKey.Should().Be(request.AssetKey);
        storedCommand.AudioData.Should().Equal(audio.AudioData);
    }

    [Fact]
    public async Task GenerateAsync_TtsFailureDoesNotStoreAnything()
    {
        var request = Request();
        _tts.Setup(x => x.SynthesizeSpeechAsync(request.ApprovedScriptText, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TextToSpeechException("Azure unavailable"));

        var act = () => new AudioAssetGenerationService(_tts.Object, _storage.Object).GenerateAsync(request);

        await act.Should().ThrowAsync<TextToSpeechException>();
        _storage.Verify(x => x.StoreAudioAsync(It.IsAny<StoreAudioRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GenerateAsync_StorageFailureDoesNotClaimSuccess()
    {
        var request = Request();
        _tts.Setup(x => x.SynthesizeSpeechAsync(request.ApprovedScriptText, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AudioSynthesisResult([0x49, 0x44, 0x33, 0x01], "audio/mpeg",
                TimeSpan.FromSeconds(10), "vi-VN-HoaiMyNeural", 4));
        _storage.Setup(x => x.StoreAudioAsync(It.IsAny<StoreAudioRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AudioStorageException("Upload failed"));

        var act = () => new AudioAssetGenerationService(_tts.Object, _storage.Object).GenerateAsync(request);

        await act.Should().ThrowAsync<AudioStorageException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GenerateAsync_EmptyScriptDoesNotCallProvider(string script)
    {
        var request = Request() with { ApprovedScriptText = script };

        var act = () => new AudioAssetGenerationService(_tts.Object, _storage.Object).GenerateAsync(request);

        await act.Should().ThrowAsync<ArgumentException>();
        _tts.Verify(x => x.SynthesizeSpeechAsync(It.IsAny<string>(), It.IsAny<TextToSpeechOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GenerateAsync_CancelledTokenDoesNotCallProvider()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => new AudioAssetGenerationService(_tts.Object, _storage.Object).GenerateAsync(Request(), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _tts.Verify(x => x.SynthesizeSpeechAsync(It.IsAny<string>(), It.IsAny<TextToSpeechOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
