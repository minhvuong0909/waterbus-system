using FluentAssertions;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.Features.SightseeingAudio.Queries.GetAudioAsset;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.UnitTests.Services.AudioStorage.Fakes;
using Xunit;

namespace WaterbusSystem.UnitTests.SightseeingAudio;

public class GetAudioAssetQueryHandlerTests
{
    private const string AssetKey = "audio-assets/tts/20261023/0a1b2c3d.mp3";
    private static readonly byte[] Mp3 = { 0x49, 0x44, 0x33, 0x04 };

    private static async Task<FakeAudioStorageService> StorageWithAssetAsync()
    {
        var storage = new FakeAudioStorageService();
        await storage.StoreAudioAsync(new StoreAudioCommand
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
    public async Task Handle_StoredAsset_ReturnsMp3Bytes()
    {
        var handler = new GetAudioAssetQueryHandler(await StorageWithAssetAsync());

        var result = await handler.Handle(new GetAudioAssetQuery(AssetKey), CancellationToken.None);

        result.Data.Should().Equal(Mp3);
        result.ContentType.Should().Be("audio/mpeg");
    }

    [Fact]
    public async Task Handle_UnknownAsset_ThrowsNotFound()
    {
        var handler = new GetAudioAssetQueryHandler(await StorageWithAssetAsync());

        var act = () => handler.Handle(new GetAudioAssetQuery("audio-assets/tts/20261023/missing.mp3"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_StorageFailure_PropagatesAudioStorageException()
    {
        var handler = new GetAudioAssetQueryHandler(new FakeAudioStorageService(simulateFailure: true));

        var act = () => handler.Handle(new GetAudioAssetQuery(AssetKey), CancellationToken.None);

        await act.Should().ThrowAsync<AudioStorageException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("secrets/config.json")]
    [InlineData("audio-assets/tts/20261023/clip.wav")]
    [InlineData("audio-assets/../private/clip.mp3")]
    public void Validator_KeyOutsideAudioAssetsOrNotMp3_FailsValidation(string assetKey)
    {
        var result = new GetAudioAssetQueryValidator().Validate(new GetAudioAssetQuery(assetKey));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_GeneratedTtsKey_PassesValidation()
    {
        var result = new GetAudioAssetQueryValidator().Validate(new GetAudioAssetQuery(AssetKey));

        result.IsValid.Should().BeTrue();
    }
}
