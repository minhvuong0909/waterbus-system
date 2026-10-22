using FluentAssertions;
using Moq;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.Features.SightseeingAudio.Commands.SynthesizeAudio;
using Xunit;

namespace WaterbusSystem.UnitTests.SightseeingAudio;

public class SynthesizeAudioCommandHandlerTests
{
    private readonly Mock<ITextToSpeechService> _tts = new();
    private readonly Mock<IAudioStorageService> _storage = new();
    private readonly Mock<IDateTimeService> _dateTime = new();
    private static readonly DateTimeOffset FixedNow = new(2026, 10, 22, 10, 0, 0, TimeSpan.Zero);

    public SynthesizeAudioCommandHandlerTests()
    {
        _dateTime.Setup(d => d.Now).Returns(FixedNow);
    }

    private SynthesizeAudioCommandHandler CreateHandler() =>
        new(_tts.Object, _storage.Object, _dateTime.Object);

    [Fact]
    public async Task Handle_ValidText_CallsTtsAndStorageAndReturnsMetadata()
    {
        var text = "Chào mừng quý khách đến với tuyến tàu buýt đường sông Sài Gòn.";
        var fakeAudioData = new byte[] { 0x49, 0x44, 0x33, 0x01 };
        var audioSynthesis = new AudioSynthesisResult(
            fakeAudioData,
            "audio/mpeg",
            TimeSpan.FromSeconds(15.2),
            "vi-VN-HoaiMyNeural",
            fakeAudioData.Length);

        _tts.Setup(t => t.SynthesizeSpeechAsync(text, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(audioSynthesis);

        StoreAudioCommand? capturedStoreCmd = null;
        _storage.Setup(s => s.StoreAudioAsync(It.IsAny<StoreAudioCommand>(), It.IsAny<CancellationToken>()))
            .Callback<StoreAudioCommand, CancellationToken>((cmd, _) => capturedStoreCmd = cmd)
            .ReturnsAsync(new AudioStorageResult(
                "https://storage.example.com/audio-assets/tts/20261022/test.mp3",
                "audio/mpeg",
                fakeAudioData.Length,
                FixedNow));

        var command = new SynthesizeAudioCommand(text);
        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.AudioUrl.Should().Be("https://storage.example.com/audio-assets/tts/20261022/test.mp3");
        result.ContentType.Should().Be("audio/mpeg");
        result.ByteLength.Should().Be(fakeAudioData.Length);
        result.DurationSeconds.Should().Be(16); // Math.Ceiling(15.2) = 16
        result.VoiceName.Should().Be("vi-VN-HoaiMyNeural");
        result.AssetKey.Should().StartWith("audio-assets/tts/20261022/");

        capturedStoreCmd.Should().NotBeNull();
        capturedStoreCmd!.AudioData.Should().Equal(fakeAudioData);
        capturedStoreCmd.ContentType.Should().Be("audio/mpeg");
        capturedStoreCmd.LanguageCode.Should().Be("vi");
    }

    [Fact]
    public async Task Handle_WithCustomVoiceName_PassesOptionToTts()
    {
        var text = "Thử giọng đọc Nam Minh.";
        var audioSynthesis = new AudioSynthesisResult(
            new byte[] { 0x01, 0x02 },
            "audio/mpeg",
            TimeSpan.FromSeconds(5),
            "vi-VN-NamMinhNeural",
            2);

        _tts.Setup(t => t.SynthesizeSpeechAsync(
                text,
                It.Is<TextToSpeechOptions>(opt => opt.VoiceName == "vi-VN-NamMinhNeural"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(audioSynthesis);

        _storage.Setup(s => s.StoreAudioAsync(It.IsAny<StoreAudioCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AudioStorageResult("https://storage.example.com/audio.mp3", "audio/mpeg", 2, FixedNow));

        var command = new SynthesizeAudioCommand(text, "vi-VN-NamMinhNeural");
        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.VoiceName.Should().Be("vi-VN-NamMinhNeural");
        _tts.Verify(t => t.SynthesizeSpeechAsync(
            text,
            It.Is<TextToSpeechOptions>(opt => opt.VoiceName == "vi-VN-NamMinhNeural"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_TtsThrowsException_PropagatesExceptionWithoutStoring()
    {
        _tts.Setup(t => t.SynthesizeSpeechAsync(It.IsAny<string>(), It.IsAny<TextToSpeechOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TextToSpeechException("Azure Speech API unreachable"));

        var command = new SynthesizeAudioCommand("Nội dung kịch bản.");
        var act = () => CreateHandler().Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<TextToSpeechException>().WithMessage("*Azure Speech API unreachable*");
        _storage.Verify(s => s.StoreAudioAsync(It.IsAny<StoreAudioCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_StorageThrowsException_PropagatesException()
    {
        _tts.Setup(t => t.SynthesizeSpeechAsync(It.IsAny<string>(), It.IsAny<TextToSpeechOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AudioSynthesisResult(new byte[] { 0x01 }, "audio/mpeg", TimeSpan.FromSeconds(1), "vi-VN-HoaiMyNeural", 1));

        _storage.Setup(s => s.StoreAudioAsync(It.IsAny<StoreAudioCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AudioStorageException("Blob storage write failed"));

        var command = new SynthesizeAudioCommand("Nội dung kịch bản.");
        var act = () => CreateHandler().Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<AudioStorageException>().WithMessage("*Blob storage write failed*");
    }

    [Fact]
    public void Validator_ValidText_PassesValidation()
    {
        var validator = new SynthesizeAudioCommandValidator();
        var command = new SynthesizeAudioCommand("Kịch bản thuyết minh hợp lệ.");

        var result = validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validator_EmptyOrWhitespaceText_FailsValidation(string? invalidText)
    {
        var validator = new SynthesizeAudioCommandValidator();
        var command = new SynthesizeAudioCommand(invalidText!);

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(SynthesizeAudioCommand.Text));
    }

    [Fact]
    public void Validator_TextExceeding10000Characters_FailsValidation()
    {
        var validator = new SynthesizeAudioCommandValidator();
        var command = new SynthesizeAudioCommand(new string('A', 10_001));

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(SynthesizeAudioCommand.Text));
    }

    [Fact]
    public void Validator_TextExactly10000Characters_PassesValidation()
    {
        var validator = new SynthesizeAudioCommandValidator();
        var command = new SynthesizeAudioCommand(new string('A', 10_000));

        var result = validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }
}
