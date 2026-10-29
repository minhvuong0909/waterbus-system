using WaterbusSystem.Application.DTOs.Requests.SightseeingAudio;
using WaterbusSystem.Application.DTOs.Responses.SightseeingAudio;
using WaterbusSystem.Application.Services.SightseeingAudio;
using WaterbusSystem.Application.Validators.SightseeingAudio;
using FluentAssertions;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.UnitTests.Services.AudioStorage.Fakes;
using Xunit;

namespace WaterbusSystem.UnitTests.SightseeingAudio;

public class AudioAssetServiceTests
{
    private const string AssetKey = "audio-assets/tts/20261023/0a1b2c3d.mp3";
    private static readonly byte[] Mp3 = { 0x49, 0x44, 0x33, 0x04 };

    private static async Task<FakeAudioStorageService> StorageWithAssetAsync()
    {
        var storage = new FakeAudioStorageService();
        await storage.StoreAudioAsync(new StoreAudioRequest
        {
            AudioData = Mp3,
            AssetKey = AssetKey,
            ContentType = "audio/mpeg",
            LanguageCode = "vi",
            Duration = TimeSpan.FromSeconds(2)
        });
        return storage;
    }

    [Fact]
    public async Task GetAsync_StoredAsset_ReturnsMp3Bytes()
    {
        var handler = new AudioAssetService(await StorageWithAssetAsync(), new GetAudioAssetRequestValidator());

        var result = await handler.GetAsync(new GetAudioAssetRequest(AssetKey), CancellationToken.None);

        result.Data.Should().Equal(Mp3);
        result.ContentType.Should().Be("audio/mpeg");
    }

    [Fact]
    public async Task GetAsync_UnknownAsset_ThrowsNotFound()
    {
        var handler = new AudioAssetService(await StorageWithAssetAsync(), new GetAudioAssetRequestValidator());

        var act = () => handler.GetAsync(new GetAudioAssetRequest("audio-assets/tts/20261023/missing.mp3"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetAsync_StorageFailure_PropagatesAudioStorageException()
    {
        var handler = new AudioAssetService(new FakeAudioStorageService(simulateFailure: true), new GetAudioAssetRequestValidator());

        var act = () => handler.GetAsync(new GetAudioAssetRequest(AssetKey), CancellationToken.None);

        await act.Should().ThrowAsync<AudioStorageException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("secrets/config.json")]
    [InlineData("audio-assets/tts/20261023/clip.wav")]
    [InlineData("audio-assets/../private/clip.mp3")]
    public void Validator_KeyOutsideAudioAssetsOrNotMp3_FailsValidation(string assetKey)
    {
        var result = new GetAudioAssetRequestValidator().Validate(new GetAudioAssetRequest(assetKey));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_GeneratedTtsKey_PassesValidation()
    {
        var result = new GetAudioAssetRequestValidator().Validate(new GetAudioAssetRequest(AssetKey));

        result.IsValid.Should().BeTrue();
    }
}
