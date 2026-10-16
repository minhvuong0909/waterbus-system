using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.Common.Validators;
using WaterbusSystem.Infrastructure.Services.AudioStorage;
using Xunit;

namespace WaterbusSystem.UnitTests.Services.AudioStorage;

public class AzureBlobAudioStorageServiceTests
{
    private static readonly Uri ContainerUri = new("https://storage.example.com/audio-guides-private");
    private static readonly Uri BlobUri = new("https://storage.example.com/audio-guides-private/pois/poi-1/vi/v1.mp3");

    private static StoreAudioCommand ValidCommand() => new()
    {
        AssetKey = "pois/poi-1/vi/v1.mp3",
        AudioData = [0x49, 0x44, 0x33, 0x01],
        ContentType = "audio/mpeg",
        LanguageCode = "vi"
    };

    private static (AzureBlobAudioStorageService Service, Mock<BlobClient> Blob) CreateService(Uri? blobUri = null)
    {
        var container = new Mock<BlobContainerClient>();
        var blob = new Mock<BlobClient>();
        container.SetupGet(c => c.Uri).Returns(ContainerUri);
        blob.SetupGet(b => b.Uri).Returns(blobUri ?? BlobUri);
        container.Setup(c => c.GetBlobClient(It.IsAny<string>())).Returns(blob.Object);
        var service = new AzureBlobAudioStorageService(
            container.Object,
            new StoreAudioCommandValidator(),
            NullLogger<AzureBlobAudioStorageService>.Instance);
        return (service, blob);
    }

    [Fact]
    public async Task StoreAudioAsync_UploadsMp3WithoutOverwriteAndReturnsDurableUri()
    {
        var (service, blob) = CreateService();
        BlobUploadOptions? seenOptions = null;
        blob.Setup(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<BlobUploadOptions>(), It.IsAny<CancellationToken>()))
            .Callback<Stream, BlobUploadOptions, CancellationToken>((_, options, _) => seenOptions = options)
            .ReturnsAsync(Response.FromValue<BlobContentInfo>(null!, Mock.Of<Response>()));

        var result = await service.StoreAudioAsync(ValidCommand());

        result.DurableAssetUri.Should().Be(BlobUri.AbsoluteUri);
        result.DurableAssetUri.Should().NotContain("?");
        result.ContentType.Should().Be("audio/mpeg");
        result.ByteLength.Should().Be(4);
        seenOptions.Should().NotBeNull();
        seenOptions!.HttpHeaders.ContentType.Should().Be("audio/mpeg");
        seenOptions.Conditions.IfNoneMatch.Should().Be(ETag.All);
        blob.Verify(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<BlobUploadOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StoreAudioAsync_ExistingAssetRefusesOverwrite()
    {
        var (service, blob) = CreateService();
        blob.Setup(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<BlobUploadOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(412, "condition failed"));

        var act = () => service.StoreAudioAsync(ValidCommand());

        await act.Should().ThrowAsync<AudioStorageException>().WithMessage("*không ghi đè*");
    }

    [Fact]
    public async Task StoreAudioAsync_RejectsSasUriBeforeUpload()
    {
        var (service, blob) = CreateService(new Uri(BlobUri + "?sig=secret"));

        var act = () => service.StoreAudioAsync(ValidCommand());

        await act.Should().ThrowAsync<AudioStorageException>().WithMessage("*SAS URL*");
        blob.Verify(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<BlobUploadOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StoreAudioAsync_InvalidMp3DoesNotCallAzure()
    {
        var (service, blob) = CreateService();

        var act = () => service.StoreAudioAsync(ValidCommand() with { AudioData = [0x01, 0x02] });

        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
        blob.Verify(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<BlobUploadOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExistsAsync_AcceptsOnlyKeyOrUriFromConfiguredContainer()
    {
        var (service, blob) = CreateService();
        blob.Setup(b => b.ExistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(true, Mock.Of<Response>()));

        (await service.ExistsAsync(ValidCommand().AssetKey)).Should().BeTrue();
        (await service.ExistsAsync(BlobUri.AbsoluteUri)).Should().BeTrue();
        (await service.ExistsAsync("https://other.example.com/audio-guides-private/pois/poi-1/vi/v1.mp3")).Should().BeFalse();
        (await service.ExistsAsync(BlobUri + "?sig=secret")).Should().BeFalse();
        blob.Verify(b => b.ExistsAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task StoreAudioAsync_CancelledTokenDoesNotCallAzure()
    {
        var (service, blob) = CreateService();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => service.StoreAudioAsync(ValidCommand(), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        blob.Verify(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<BlobUploadOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
