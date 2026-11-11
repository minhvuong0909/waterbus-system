using WaterbusSystem.Application.DTOs.Requests.SightseeingAudio;
using FluentAssertions;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.Validators.SightseeingAudio;
using Xunit;

namespace WaterbusSystem.UnitTests.Services.AudioStorage;

public class AudioStorageValidationTests
{
    private readonly StoreAudioRequestValidator _validator = new();

    private static StoreAudioRequest CreateValidCommand() => new()
    {
        AudioData = new byte[] { 0xFF, 0xFB, 0x90, 0x64 }, // sample MP3 bytes
        AssetKey = "pois/11111111-1111-1111-1111-111111111111/vi.mp3",
        ContentType = "audio/mpeg",
        LanguageCode = "vi",
        PoiId = Guid.NewGuid(),
        Duration = TimeSpan.FromSeconds(30)
    };

    [Fact]
    public void Validate_ValidCommand_ShouldPass()
    {
        var command = CreateValidCommand();

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    public void Validate_NullAudioData_ShouldFail(byte[]? nullData)
    {
        var command = CreateValidCommand() with { AudioData = nullData! };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "AudioData");
    }

    [Fact]
    public void Validate_EmptyAudioData_ShouldFail()
    {
        var command = CreateValidCommand() with { AudioData = Array.Empty<byte>() };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "AudioData");
    }

    [Fact]
    public void Validate_NonMp3Bytes_ShouldFail()
    {
        var result = _validator.Validate(CreateValidCommand() with
        {
            AudioData = new byte[] { 0x01, 0x02, 0x03, 0x04 }
        });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "AudioData");
    }

    [Fact]
    public void Validate_AudioDataTooLarge_ShouldFail()
    {
        // 51 MB dummy array
        var oversizedData = new byte[51 * 1024 * 1024];
        var command = CreateValidCommand() with { AudioData = oversizedData };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("50MB"));
    }

    [Theory]
    [InlineData("text/plain")]
    [InlineData("video/mp4")]
    [InlineData("audio/wav")]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_InvalidContentType_ShouldFail(string? invalidContentType)
    {
        var command = CreateValidCommand() with { ContentType = invalidContentType! };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ContentType");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("pois/test.wav")] // không phải .mp3
    [InlineData("../pois/test.mp3")] // path traversal
    [InlineData("pois/../test.mp3")] // path traversal
    [InlineData("pois\\test.mp3")] // backslash
    [InlineData("/pois/test.mp3")] // leading slash
    [InlineData("pois//test.mp3")] // double slash
    [InlineData("pois/test.mp3?sig=secret")]
    [InlineData(null)]
    public void Validate_InvalidAssetKey_ShouldFail(string? invalidKey)
    {
        var command = CreateValidCommand() with { AssetKey = invalidKey! };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "AssetKey");
    }

    [Fact]
    public void Validate_AssetKeyExceeding400Chars_ShouldFail()
    {
        var longKey = new string('a', 397) + ".mp3"; // 401 ký tự
        var command = CreateValidCommand() with { AssetKey = longKey };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "AssetKey");
    }

    [Theory]
    [InlineData("en")]
    [InlineData("fr")]
    [InlineData("")]
    public void Validate_InvalidLanguageCode_ShouldFail(string invalidLang)
    {
        var command = CreateValidCommand() with { LanguageCode = invalidLang };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "LanguageCode");
    }

    [Fact]
    public void Validate_EmptyPoiId_ShouldFail()
    {
        var command = CreateValidCommand() with { PoiId = Guid.Empty };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "PoiId");
    }

    [Fact]
    public void Validate_NonPositiveDuration_ShouldFail()
    {
        var result = _validator.Validate(CreateValidCommand() with { Duration = TimeSpan.Zero });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Duration");
    }
}
