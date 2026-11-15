using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.SightseeingAudio.Interfaces;
using WaterbusSystem.Application.Features.Incidents;
using WaterbusSystem.Application.Features.SightseeingAudio.Models;
using WaterbusSystem.Application.Services.SightseeingAudio;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Infrastructure.Services;
using WaterbusSystem.Infrastructure.Services.SightseeingAudio;
using WaterbusSystem.UnitTests.SightseeingAudio.Fakes;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;

namespace WaterbusSystem.UnitTests.SightseeingAudio;

public class TrackingAudioTriggerServiceTests
{
    private readonly Infrastructure.Persistence.ApplicationDbContext _context = TestDbContextFactory.CreateDbContext();
    private readonly TestDbContextFactory.TripSeedData _seed;
    private readonly RecordingOnboardAudioTriggerDispatcher _dispatcher = new();
    private readonly Fakes.InMemoryPoiTriggerDeduplicationService _deduplication = new();

    public TrackingAudioTriggerServiceTests()
    {
        _seed = TestDbContextFactory.SeedTrip(_context);
        _seed.Trip.TripType = TripType.Sightseeing;
        _seed.Trip.Status = TripStatus.EnRoute;
        var poi = new PointOfInterest
        {
            Name = "Bến Nhà Rồng", Latitude = 10.7769m, Longitude = 106.7009m
        };
        _context.PointsOfInterest.Add(poi);
        _context.RoutePois.Add(new RoutePoi
        {
            RouteId = _seed.Route.Id, PoiId = poi.Id, Poi = poi,
            DisplayOrder = 1, TriggerRadiusM = 100
        });
        _context.AudioGuides.Add(new AudioGuide
        {
            PoiId = poi.Id, Poi = poi, LanguageCode = "vi", IsPublished = true,
            AudioAssetUri = "https://example.blob.core.windows.net/private/poi.mp3"
        });
        foreach (var language in new[] { "vi", "en" })
            _context.AudioClips.Add(new AudioClip
            {
                Kind = AudioClipKind.Poi, SubjectId = poi.Id, LanguageCode = language, Text = "Bến Nhà Rồng",
                Data = [1, 2, 3], DurationSeconds = 5, IsPublished = true
            });
        _context.SaveChanges();
    }

    private TrackingAudioTriggerService CreateEvaluator(IOnboardAudioTriggerDispatcher? dispatcher = null,
        IPoiTriggerDeduplicationService? deduplication = null) => new(
        new WaterbusSystem.Infrastructure.Repositories.SightseeingAudioRepository(_context),
        new PoiAudioTriggerDecisionService(new NtsGeoDistanceCalculator(),
            deduplication ?? _deduplication, dispatcher ?? _dispatcher,
            NullLogger<PoiAudioTriggerDecisionService>.Instance),
        new StationAnnouncementDecisionService(new NtsGeoDistanceCalculator(), deduplication ?? _deduplication,
            dispatcher ?? _dispatcher, Microsoft.Extensions.Options.Options.Create(new WaterbusSystem.Application.Features.SightseeingAudio.SightseeingAudioOptions()),
            NullLogger<StationAnnouncementDecisionService>.Instance),
        deduplication ?? _deduplication);

    private BoatPositionSnapshot Position() => new()
    {
        BoatId = _seed.Boat.Id, TripId = _seed.Trip.Id,
        Latitude = 10.7769m, Longitude = 106.7009m,
        ObservedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task Evaluate_EnRouteSightseeing_ShouldDispatchOnceForPublishedPoi()
    {
        var evaluator = CreateEvaluator();

        var first = await evaluator.EvaluateAsync(Position());
        var duplicate = await evaluator.EvaluateAsync(Position());

        first!.ShouldTrigger.Should().BeTrue();
        duplicate.Should().BeNull("mọi địa danh trên tuyến đã phát nên không còn địa danh kế tiếp để xét");
        _dispatcher.DispatchedCount.Should().Be(1);
    }

    [Fact]
    public async Task Evaluate_OpenSafetySos_ShouldNotDispatchEvenWhenTripEnRoute()
    {
        var incident = new Incident
        {
            ScopeType = IncidentConstants.Scopes.Trip,
            IncidentType = IncidentConstants.Types.Safety,
            Status = IncidentConstants.Statuses.Open
        };
        _context.Incidents.Add(incident);
        _context.IncidentTrips.Add(new IncidentTrip { Incident = incident, TripId = _seed.Trip.Id });
        _context.SaveChanges();

        var result = await CreateEvaluator().EvaluateAsync(Position());

        result!.Reason.Should().Be(TriggerDecisionReason.SafetyAnnouncementActive);
        _dispatcher.DispatchedCount.Should().Be(0);
    }

    [Theory]
    [InlineData(TripStatus.Suspended, TripType.Sightseeing)]
    [InlineData(TripStatus.EnRoute, TripType.Commuter)]
    public async Task Evaluate_NonEligibleTrip_ShouldNotDispatch(TripStatus status, TripType type)
    {
        _seed.Trip.Status = status;
        _seed.Trip.TripType = type;
        _context.SaveChanges();

        var result = await CreateEvaluator().EvaluateAsync(Position());

        result.Should().BeNull();
        _dispatcher.DispatchedCount.Should().Be(0);
    }

    [Fact]
    public async Task Evaluate_MissingRadius_ShouldNotDispatch()
    {
        _context.RoutePois.Single().TriggerRadiusM = null;
        _context.SaveChanges();

        var result = await CreateEvaluator().EvaluateAsync(Position());

        result!.Reason.Should().Be(TriggerDecisionReason.TriggerRadiusMissingOrInvalid);
        _dispatcher.DispatchedCount.Should().Be(0);
    }

    [Fact]
    public async Task Evaluate_WithLogOnlyDispatcher_ShouldNotClaimTriggerOrPoisonDeduplication()
    {
        var dedup = new WaterbusSystem.Infrastructure.Services.SightseeingAudio.InMemoryPoiTriggerDeduplicationService();
        var evaluator = CreateEvaluator(
            new LogOnlyAudioTriggerDispatcher(NullLogger<LogOnlyAudioTriggerDispatcher>.Instance), dedup);

        var first = await evaluator.EvaluateAsync(Position());
        var retry = await evaluator.EvaluateAsync(Position());

        first!.Reason.Should().Be(TriggerDecisionReason.DispatchFailed);
        retry!.Reason.Should().Be(TriggerDecisionReason.DispatchFailed);
        (await dedup.HasBeenTriggeredAsync(_seed.Trip.Id, _context.RoutePois.Single().Id)).Should().BeFalse();
    }
}
