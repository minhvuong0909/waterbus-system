using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WaterbusSystem.Application.Features.SightseeingAudio;
using WaterbusSystem.Application.Features.SightseeingAudio.Models;
using WaterbusSystem.Application.Services.SightseeingAudio;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Infrastructure.Services;
using WaterbusSystem.Infrastructure.Services.Tracking;
using WaterbusSystem.UnitTests.SightseeingAudio.Fakes;
using Xunit;

namespace WaterbusSystem.UnitTests.SightseeingAudio;

public class StationAnnouncementTests
{
    private static readonly IReadOnlyList<AudioClipRef> Clips = [new("vi", 4, "vi"), new("en", 4, "en")];
    private readonly RecordingOnboardAudioTriggerDispatcher _dispatcher = new();
    private readonly Guid _boatId = Guid.NewGuid();
    private readonly Guid _tripId = Guid.NewGuid();

    private TripStationContext Context(TripType type, params (string Name, decimal Lat)[] stops) => new(
        new TripSightseeingContext { TripId = _tripId, BoatId = _boatId, RouteId = Guid.NewGuid(), TripType = type, Status = TripStatus.EnRoute },
        stops.Select((s, i) => new TripStopInfo(Guid.NewGuid(), Guid.NewGuid(), s.Name, i + 1, s.Lat, 106.7m, Clips, Clips, Clips)).ToArray());

    private StationAnnouncementDecisionService Service() => new(new NtsGeoDistanceCalculator(), new InMemoryPoiTriggerDeduplicationService(),
        _dispatcher, Options.Create(new SightseeingAudioOptions()), NullLogger<StationAnnouncementDecisionService>.Instance);

    private async Task Walk(StationAnnouncementDecisionService service, TripStationContext context, params decimal[] latitudes)
    {
        foreach (var lat in latitudes)
            await service.EvaluateAndTriggerAsync(new BoatPositionSnapshot
            { BoatId = _boatId, TripId = _tripId, Latitude = lat, Longitude = 106.7m, ObservedAt = DateTimeOffset.UtcNow }, context);
    }

    // A ở 10.00, B ở 10.02 (~2,2 km), C ở 10.04. 0,001 độ ≈ 111 m.
    private static readonly decimal[] FullRun =
    [
        10.0000m, 10.0000m, 10.0020m,                 // ở bến A rồi rời xa (>120 m)
        10.0150m, 10.0190m, 10.0200m, 10.0200m,       // sắp tới B (<500 m), tới B (<60 m)
        10.0220m, 10.0300m,                           // rời B
        10.0360m, 10.0400m, 10.0400m                  // sắp tới C, tới C
    ];

    [Fact]
    public async Task Commuter_FullRun_EmitsEachAnnouncementOnceInOrder()
    {
        var context = Context(TripType.Commuter, ("A", 10.00m), ("B", 10.02m), ("C", 10.04m));

        await Walk(Service(), context, FullRun);

        _dispatcher.DispatchedRequests.Select(r => $"{r.Kind}:{r.SubjectName}").Should().Equal(
            "StationDeparted:A", "StationApproaching:B", "StationArrived:B", "StationDeparted:B",
            "StationApproaching:C", "StationArrived:C");
    }

    [Fact]
    public async Task Sightseeing_OnlyDepartsOriginAndApproachesDestination()
    {
        var context = Context(TripType.Sightseeing, ("A", 10.00m), ("C", 10.04m));

        await Walk(Service(), context, 10.0000m, 10.0030m, 10.0360m, 10.0400m);

        _dispatcher.DispatchedRequests.Select(r => $"{r.Kind}:{r.SubjectName}").Should().Equal(
            "StationDeparted:A", "StationApproaching:C", "StationArrived:C");
    }

    [Fact]
    public async Task NotEnRoute_NothingIsPlayed()
    {
        var context = Context(TripType.Commuter, ("A", 10.00m), ("B", 10.02m));
        context = context with { Trip = context.Trip with { Status = TripStatus.Suspended } };

        await Walk(Service(), context, FullRun);

        _dispatcher.DispatchedCount.Should().Be(0);
    }

    [Fact]
    public void DemoTimeline_HoldsAtEveryStopAndFinishesTheLoop()
    {
        double[] stops = [0, 0.4, 1];

        DemoTimeline.At(stops, 210, 8, 0).Should().Be((0, true));                  // đứng ở bến đầu
        DemoTimeline.At(stops, 210, 8, 209.9).Fraction.Should().Be(1);             // đứng ở bến cuối
        var middle = Enumerable.Range(0, 2100).Select(i => DemoTimeline.At(stops, 210, 8, i / 10.0))
            .Where(p => p.Dwelling && Math.Abs(p.Fraction - 0.4) < 1e-9).Count();
        middle.Should().BeGreaterThan(70);                                          // ~8 giây đứng yên ở bến giữa
        DemoTimeline.At(stops, 210, 8, 210 + 50).Should().Be(DemoTimeline.At(stops, 210, 8, 50)); // lặp vòng
    }
}
