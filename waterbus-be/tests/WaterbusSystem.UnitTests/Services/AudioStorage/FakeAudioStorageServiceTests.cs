using WaterbusSystem.Application.DTOs.Requests.SightseeingAudio;
using FluentAssertions;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.UnitTests.Services.AudioStorage.Fakes;
using Xunit;

namespace WaterbusSystem.UnitTests.Services.AudioStorage;

public class FakeAudioStorageServiceTests
{
    private readonly FakeAudioStorageService _storage = new();

    private static StoreAudioRequest CreateSampleCommand() => new()
    {
        AudioData = new byte[] { 0xFF, 0xFB, 0x90, 0x64, 0x01, 0x02, 0x03 },
        AssetKey = "pois/sample-poi-id/vi.mp3",
        ContentType = "audio/mpeg",
        LanguageCode = "vi"
    };

    [Fact]
    public async Task StoreAudioAsync_ValidCommand_ReturnsDurableUriWithoutSasToken()
    {
        var command = CreateSampleCommand();

        var result = await _storage.StoreAudioAsync(command);

        result.Should().NotBeNull();
        result.DurableAssetUri.Should().NotBeNullOrEmpty();
        result.DurableAssetUri.Length.Should().BeLessThanOrEqualTo(500, "phải vừa cột nvarchar(500) của AudioGuide.AudioAssetUri");

        // Tuyệt đối KHÔNG chứa query string hoặc SAS token (dấu ?, &sig=, &se=)
        result.DurableAssetUri.Should().NotContain("?");
        result.DurableAssetUri.Should().NotContain("&sig=");
        result.DurableAssetUri.Should().NotContain("&se=");

        // Tuyệt đối KHÔNG phải đường dẫn file tạm thời cục bộ
        result.DurableAssetUri.Should().NotStartWith("C:\\");
        result.DurableAssetUri.Should().NotStartWith("/tmp");

        result.ContentType.Should().Be("audio/mpeg");
        result.ByteLength.Should().Be(command.AudioData.Length);
    }

    [Fact]
    public async Task ExistsAsync_AfterStoring_ReturnsTrue()
    {
        var command = CreateSampleCommand();

        var storeResult = await _storage.StoreAudioAsync(command);

        var existsByUri = await _storage.ExistsAsync(storeResult.DurableAssetUri);
        var existsByKey = await _storage.ExistsAsync(command.AssetKey);

        existsByUri.Should().BeTrue();
        existsByKey.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_NonExistentAsset_ReturnsFalse()
    {
        var exists = await _storage.ExistsAsync("pois/non-existent/vi.mp3");

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsAsync_EmptyIdentifier_ReturnsFalse()
    {
        var exists = await _storage.ExistsAsync("");

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task StoreAudioAsync_InvalidCommand_ThrowsValidationException()
    {
        var invalidCommand = CreateSampleCommand() with { AudioData = Array.Empty<byte>() };

        var act = () => _storage.StoreAudioAsync(invalidCommand);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task StoreAudioAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => _storage.StoreAudioAsync(CreateSampleCommand(), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task StoreAudioAsync_SimulatedFailure_ThrowsAudioStorageException()
    {
        var failingStorage = new FakeAudioStorageService(simulateFailure: true);

        var act = () => failingStorage.StoreAudioAsync(CreateSampleCommand());

        await act.Should().ThrowAsync<AudioStorageException>()
            .WithMessage("*lỗi kết nối*");
    }

    [Fact]
    public async Task StoreAudioAsync_SameKeyAndSameBytes_IsIdempotent()
    {
        var command = CreateSampleCommand();

        var first = await _storage.StoreAudioAsync(command);
        var retry = await _storage.StoreAudioAsync(command);

        retry.DurableAssetUri.Should().Be(first.DurableAssetUri);
        _storage.GetStoredBytes(command.AssetKey).Should().BeEquivalentTo(command.AudioData);
    }

    [Fact]
    public async Task StoreAudioAsync_SameKeyAndDifferentBytes_RejectsOverwrite()
    {
        var initialCommand = CreateSampleCommand();
        var updatedCommand = initialCommand with { AudioData = new byte[] { 0xFF, 0xFB, 0x90, 0x64, 0x09 } };

        await _storage.StoreAudioAsync(initialCommand);
        var act = () => _storage.StoreAudioAsync(updatedCommand);

        await act.Should().ThrowAsync<AudioStorageException>().WithMessage("*nội dung khác*");
        _storage.GetStoredBytes(initialCommand.AssetKey).Should().BeEquivalentTo(initialCommand.AudioData);
    }

    [Fact]
    public async Task StoreAudioAsync_TooLongUri_DoesNotStoreBytes()
    {
        var storage = new FakeAudioStorageService("fake-audio://" + new string('a', 480));
        var command = CreateSampleCommand();
        var act = () => storage.StoreAudioAsync(command);

        await act.Should().ThrowAsync<AudioStorageException>();
        (await storage.ExistsAsync(command.AssetKey)).Should().BeFalse();
    }

    [Fact]
    public async Task ExistsAsync_OtherStoragePrefix_ReturnsFalse()
    {
        var command = CreateSampleCommand();
        await _storage.StoreAudioAsync(command);

        var exists = await _storage.ExistsAsync("fake-audio://audio-guides-private-other/" + command.AssetKey);

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task GetStoredBytes_ReturnsCopy()
    {
        var command = CreateSampleCommand();
        await _storage.StoreAudioAsync(command);

        var copy = _storage.GetStoredBytes(command.AssetKey)!;
        copy[0] = 0x00;

        _storage.GetStoredBytes(command.AssetKey).Should().BeEquivalentTo(command.AudioData);
    }
}
