using WaterbusSystem.Application.DTOs.Requests.SightseeingAudio;
using WaterbusSystem.Application.DTOs.Responses.SightseeingAudio;
using WaterbusSystem.Application.Services.SightseeingAudio;
using WaterbusSystem.Application.Validators.SightseeingAudio;
using FluentAssertions;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.UnitTests.Services.AudioStorage.Fakes;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;

namespace WaterbusSystem.UnitTests.SightseeingAudio;

public class AudioGuideServiceTests
{
    [Fact]
    public async Task GetPois_WithoutAzureCredentials_DoesNotInitializeBlobClient()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var container = new Lazy<Azure.Storage.Blobs.BlobContainerClient>(() =>
            throw new InvalidOperationException("Blob credentials are not configured."));
        var storage = new WaterbusSystem.Infrastructure.Services.AudioStorage.AzureBlobAudioStorageService(
            container, new StoreAudioRequestValidator(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<WaterbusSystem.Infrastructure.Services.AudioStorage.AzureBlobAudioStorageService>.Instance);
        var service = new AudioGuideService(new WaterbusSystem.Infrastructure.Repositories.SightseeingAudioRepository(context),
            new WaterbusSystem.Infrastructure.Persistence.EfUnitOfWork(context), storage,
            new PublishAudioGuideRequestValidator(), new GetAudioGuidePoisRequestValidator());

        var page = await service.GetPoisAsync(new GetAudioGuidePoisRequest());

        page.Items.Should().BeEmpty();
        container.IsValueCreated.Should().BeFalse();
    }

    private static AudioGuideService CreateService(WaterbusSystem.Infrastructure.Persistence.ApplicationDbContext context, FakeAudioStorageService storage) =>
        new(new WaterbusSystem.Infrastructure.Repositories.SightseeingAudioRepository(context),
            new WaterbusSystem.Infrastructure.Persistence.EfUnitOfWork(context), storage,
            new PublishAudioGuideRequestValidator(), new GetAudioGuidePoisRequestValidator());

    private const string Key = "audio-assets/tts/20261027/guide.mp3";
    private const string BlobPrefix = "https://waterbusaudio.blob.core.windows.net/private";

    private static async Task<(FakeAudioStorageService Storage, string Uri)> StoredMp3Async()
    {
        var storage = new FakeAudioStorageService(BlobPrefix);
        var stored = await storage.StoreAudioAsync(new StoreAudioRequest
        {
            AudioData = [0x49, 0x44, 0x33, 0x04], AssetKey = Key,
            ContentType = "audio/mpeg", LanguageCode = "vi",
            Duration = TimeSpan.FromSeconds(4)
        });
        return (storage, stored.DurableAssetUri);
    }

    [Fact]
    public async Task Publish_ActivePoiAndExistingMp3_CreatesPublishedGuide()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var poi = new PointOfInterest { Name = "Bến Nhà Rồng" };
        context.PointsOfInterest.Add(poi);
        await context.SaveChangesAsync();
        var (storage, uri) = await StoredMp3Async();

        var result = await CreateService(context, storage)
            .PublishAsync(new PublishAudioGuideRequest(poi.Id, uri, 4), CancellationToken.None);

        result.IsPublished.Should().BeTrue();
        result.LanguageCode.Should().Be("vi");
        context.AudioGuides.Single().AudioAssetUri.Should().Be(uri);
        context.AudioGuides.Single().PoiId.Should().Be(poi.Id);
    }

    [Fact]
    public async Task Publish_ExistingDemoGuide_ReplacesUriWithoutCreatingDuplicate()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var poi = new PointOfInterest { Name = "Bến Nhà Rồng" };
        var guide = new AudioGuide
        {
            Poi = poi, PoiId = poi.Id, LanguageCode = "vi",
            AudioAssetUri = "/audio/poi-01.mp3", IsPublished = true
        };
        context.PointsOfInterest.Add(poi);
        context.AudioGuides.Add(guide);
        await context.SaveChangesAsync();
        var (storage, uri) = await StoredMp3Async();
        var handler = CreateService(context, storage);

        var first = await handler.PublishAsync(new PublishAudioGuideRequest(poi.Id, uri, 4), CancellationToken.None);
        var repeated = await handler.PublishAsync(new PublishAudioGuideRequest(poi.Id, uri, 4), CancellationToken.None);

        first.AudioGuideId.Should().Be(guide.Id);
        repeated.AudioGuideId.Should().Be(guide.Id);
        context.AudioGuides.Should().ContainSingle();
        guide.AudioAssetUri.Should().Be(uri);
    }

    [Fact]
    public async Task Publish_MissingAsset_RejectsWithoutChangingGuide()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var poi = new PointOfInterest { Name = "Bến Nhà Rồng" };
        context.PointsOfInterest.Add(poi);
        await context.SaveChangesAsync();
        var storage = new FakeAudioStorageService(BlobPrefix);

        var act = () => CreateService(context, storage)
            .PublishAsync(new PublishAudioGuideRequest(poi.Id, BlobPrefix + "/" + Key, 4), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        context.AudioGuides.Should().BeEmpty();
    }

    [Fact]
    public async Task Publish_InactivePoi_RejectsWithoutChangingGuide()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var poi = new PointOfInterest { Name = "Đã đóng", IsActive = false };
        context.PointsOfInterest.Add(poi);
        await context.SaveChangesAsync();
        var (storage, uri) = await StoredMp3Async();

        var act = () => CreateService(context, storage)
            .PublishAsync(new PublishAudioGuideRequest(poi.Id, uri, 4), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        context.AudioGuides.Should().BeEmpty();
    }

    [Fact]
    public async Task Publish_AssetOutsideConfiguredBlob_RejectsWithoutChangingGuide()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var poi = new PointOfInterest { Name = "Bến Nhà Rồng" };
        context.PointsOfInterest.Add(poi);
        await context.SaveChangesAsync();
        var (storage, _) = await StoredMp3Async();

        var act = () => CreateService(context, storage)
            .PublishAsync(new PublishAudioGuideRequest(poi.Id,
                "https://untrusted.blob.core.windows.net/private/" + Key, 4), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        context.AudioGuides.Should().BeEmpty();
    }

    [Theory]
    [InlineData("https://other.example/audio-assets/tts/x.mp3?sig=secret")]
    [InlineData("http://other.example/audio-assets/tts/x.mp3")]
    [InlineData("https://other.example/audio-assets/tts/x.wav")]
    [InlineData("https://other.example/other/x.mp3")]
    public void Validator_UnsafeAssetUri_IsRejected(string uri)
    {
        new PublishAudioGuideRequestValidator()
            .Validate(new PublishAudioGuideRequest(Guid.NewGuid(), uri, 4))
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task GetPois_UsesDatabaseAndShowsPublishedStatus()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var seed = TestDbContextFactory.SeedTrip(context);
        var poi = new PointOfInterest { Name = "Bến Nhà Rồng" };
        context.PointsOfInterest.Add(poi);
        context.RoutePois.Add(new RoutePoi
        {
            RouteId = seed.Route.Id, PoiId = poi.Id,
            DisplayOrder = 1, TriggerRadiusM = 350, IsActive = true
        });
        context.AudioGuides.Add(new AudioGuide
        {
            Poi = poi, PoiId = poi.Id, LanguageCode = "vi", IsPublished = true,
            AudioAssetUri = BlobPrefix + "/" + Key
        });
        await context.SaveChangesAsync();

        var result = await CreateService(context, new FakeAudioStorageService())
            .GetPoisAsync(new GetAudioGuidePoisRequest(), CancellationToken.None);

        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle().Which.PoiId.Should().Be(poi.Id);
        result.Items.Single().HasActiveRoutePoi.Should().BeTrue();
        result.Items.Single().IsPublished.Should().BeTrue();
        result.Items.Single().AudioAssetUri.Should().Be(BlobPrefix + "/" + Key);
    }

    [Fact]
    public async Task GetPois_PaginatesAndExcludesInactivePois()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        context.PointsOfInterest.AddRange(
            new PointOfInterest { Name = "A" },
            new PointOfInterest { Name = "B" },
            new PointOfInterest { Name = "C", IsActive = false });
        await context.SaveChangesAsync();

        var result = await CreateService(context, new FakeAudioStorageService())
            .GetPoisAsync(new GetAudioGuidePoisRequest(2, 1), CancellationToken.None);

        result.TotalCount.Should().Be(2);
        result.Items.Should().ContainSingle().Which.Name.Should().Be("B");
    }
}
