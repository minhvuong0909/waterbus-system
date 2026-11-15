using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.Features.SightseeingAudio;
using WaterbusSystem.Application.Features.SightseeingAudio.Models;
using WaterbusSystem.Application.Features.SightseeingAudio.Services;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Infrastructure.Services;
using WaterbusSystem.Infrastructure.Services.SightseeingAudio;
using Xunit;

namespace WaterbusSystem.UnitTests.SightseeingAudio;

/// <summary>
/// Kiểm tra luồng phát thông báo bến và thuyết minh địa danh theo loại chuyến.
/// </summary>
public class StopAnnouncementTests
{
    [Fact]
    public void StopAnnouncementMessage_CarriesExpectedPayload()
    {
        var tripId = Guid.NewGuid();
        var boatId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var stationName = "Bến Bạch Đằng";
        var at = DateTimeOffset.UtcNow;

        var message = new StopAnnouncementMessage(
            tripId, boatId, stationId, stationName, "near", 0.85, at, "Commuter");

        message.TripId.Should().Be(tripId);
        message.BoatId.Should().Be(boatId);
        message.StationId.Should().Be(stationId);
        message.StationName.Should().Be(stationName);
        message.Type.Should().Be("near");
        message.SegmentProgress.Should().Be(0.85);
        message.TripType.Should().Be("Commuter");
        message.Version.Should().Be(2);
    }

    [Fact]
    public async Task StopAnnouncementPublisher_PublishesAllThreeEventTypes()
    {
        var mockPublisher = new Mock<IStopAnnouncementPublisher>();
        var tripId = Guid.NewGuid();
        var boatId = Guid.NewGuid();
        var fromStationId = Guid.NewGuid();
        var toStationId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        mockPublisher
            .Setup(p => p.PublishDepartingAsync(tripId, boatId, fromStationId, "Bến Bạch Đằng", now, "Sightseeing", It.IsAny<string?>(), It.IsAny<StopNarrationPayload?>(), default))
            .Returns(Task.CompletedTask);

        mockPublisher
            .Setup(p => p.PublishNearStopAsync(tripId, boatId, toStationId, "Bến Bình An", 0.8, now, "Sightseeing", default))
            .Returns(Task.CompletedTask);

        mockPublisher
            .Setup(p => p.PublishArrivedAsync(tripId, boatId, toStationId, "Bến Bình An", 1.0, now, "Sightseeing", default))
            .Returns(Task.CompletedTask);

        await mockPublisher.Object.PublishDepartingAsync(tripId, boatId, fromStationId, "Bến Bạch Đằng", now, "Sightseeing", nextStationName: null);
        await mockPublisher.Object.PublishNearStopAsync(tripId, boatId, toStationId, "Bến Bình An", 0.8, now, "Sightseeing");
        await mockPublisher.Object.PublishArrivedAsync(tripId, boatId, toStationId, "Bến Bình An", 1.0, now, "Sightseeing");

        mockPublisher.Verify(p => p.PublishDepartingAsync(tripId, boatId, fromStationId, "Bến Bạch Đằng", now, "Sightseeing", It.IsAny<string?>(), It.IsAny<StopNarrationPayload?>(), default), Times.Once);
        mockPublisher.Verify(p => p.PublishNearStopAsync(tripId, boatId, toStationId, "Bến Bình An", 0.8, now, "Sightseeing", default), Times.Once);
        mockPublisher.Verify(p => p.PublishArrivedAsync(tripId, boatId, toStationId, "Bến Bình An", 1.0, now, "Sightseeing", default), Times.Once);
    }

    [Fact]
    public async Task StopAnnouncementService_CommuterTrip_PublishesNoNarration()
    {
        var fixture = StopAnnouncementFixture.Create("Commuter");

        var published = await fixture.Service.AnnounceDepartingAsync(fixture.TripId, 1);

        published.Should().BeTrue();
        fixture.Publisher.Departing.Count.Should().Be(1);
        fixture.Publisher.Departing[0].Narration.Should().BeNull();
    }

    [Fact]
    public async Task StopAnnouncementService_SightseeingTrip_PublishesNarrationOnDeparting()
    {
        var fixture = StopAnnouncementFixture.Create("Sightseeing");

        var published = await fixture.Service.AnnounceDepartingAsync(fixture.TripId, 1);

        published.Should().BeTrue();
        fixture.Publisher.Departing.Count.Should().Be(1);
        var narration = fixture.Publisher.Departing[0].Narration;
        narration.Should().NotBeNull();
        narration!.PoiName.Should().Be("Cầu Ba Son");
        narration.Text.Should().Contain("Cầu Ba Son");
    }

    [Fact]
    public async Task StopAnnouncementService_SameSegment_AnnouncesOnlyOnce()
    {
        var fixture = StopAnnouncementFixture.Create("Sightseeing");

        (await fixture.Service.AnnounceDepartingAsync(fixture.TripId, 1)).Should().BeTrue();
        (await fixture.Service.AnnounceDepartingAsync(fixture.TripId, 1)).Should().BeFalse();

        fixture.Publisher.Departing.Count.Should().Be(1);
    }

    [Fact]
    public async Task StopAnnouncementService_NearStop_PublishesWithoutNarration()
    {
        var fixture = StopAnnouncementFixture.Create("Sightseeing");

        var published = await fixture.Service.AnnounceNearStopAsync(fixture.TripId, 2, 0.85);

        published.Should().BeTrue();
        fixture.Publisher.Near.Count.Should().Be(1);
        fixture.Publisher.Near[0].StationName.Should().Be("Bến Bình An");
    }

    [Fact]
    public async Task StopAnnouncementService_UnknownTrip_ReturnsFalse()
    {
        var fixture = StopAnnouncementFixture.Create("Sightseeing");

        var published = await fixture.Service.AnnounceDepartingAsync(Guid.NewGuid(), 1);

        published.Should().BeFalse();
        fixture.Publisher.Departing.Should().BeEmpty();
    }

    [Fact]
    public async Task StopAnnouncementService_NarrationLongerThanBudget_IsNotPublished()
    {
        var options = new StopAnnouncementOptions { TripDurationSeconds = 180, DwellTimeSeconds = 6 };
        var fixture = StopAnnouncementFixture.Create("Sightseeing", options);

        await fixture.Service.AnnounceDepartingAsync(fixture.TripId, 1);

        fixture.Publisher.Departing.Should().ContainSingle();
        fixture.Publisher.Departing[0].Narration.Should().NotBeNull();
        fixture.Publisher.Departing[0].Narration!.Text.Length.Should().BeLessThanOrEqualTo(
            StopAnnouncementTimeBudgetCalculator.Calculate(options, 4).MaxNarrationCharacters);
    }

    private sealed class RecordingPublisher : IStopAnnouncementPublisher
    {
        public List<(string StationName, StopNarrationPayload? Narration)> Departing { get; } = new();
        public List<(string StationName, double Progress)> Near { get; } = new();
        public List<(string StationName, double Progress)> Arrived { get; } = new();

        public Task PublishDepartingAsync(
            Guid tripId, Guid boatId, Guid stationId, string stationName, DateTimeOffset at,
            string? tripType = null, string? nextStationName = null,
            StopNarrationPayload? narration = null, CancellationToken cancellationToken = default)
        {
            Departing.Add((stationName, narration));
            return Task.CompletedTask;
        }

        public Task PublishNearStopAsync(
            Guid tripId, Guid boatId, Guid stationId, string stationName, double segmentProgress,
            DateTimeOffset at, string? tripType = null, CancellationToken cancellationToken = default)
        {
            Near.Add((stationName, segmentProgress));
            return Task.CompletedTask;
        }

        public Task PublishArrivedAsync(
            Guid tripId, Guid boatId, Guid stationId, string stationName, double segmentProgress,
            DateTimeOffset at, string? tripType = null, CancellationToken cancellationToken = default)
        {
            Arrived.Add((stationName, segmentProgress));
            return Task.CompletedTask;
        }
    }

    private sealed class StopAnnouncementFixture
    {
        public required Guid TripId { get; init; }
        public required RecordingPublisher Publisher { get; init; }
        public required IStopAnnouncementService Service { get; init; }

        public static StopAnnouncementFixture Create(string tripType) =>
            Create(tripType, new StopAnnouncementOptions());

        public static StopAnnouncementFixture Create(string tripType, StopAnnouncementOptions options)
        {
            var tripId = Guid.NewGuid();
            var publisher = new RecordingPublisher();

            var stations = new[]
            {
                new StopAnnouncementStation(Guid.NewGuid(), "Bến Bạch Đằng", 1, 10.7752301m, 106.7072821m),
                new StopAnnouncementStation(Guid.NewGuid(), "Bến Bình An", 2, 10.79736m, 106.7283885m),
                new StopAnnouncementStation(Guid.NewGuid(), "Bến Thanh Đa", 3, 10.8196062m, 106.7166367m),
            };

            var repository = new Mock<ISightseeingAudioRepository>();
            repository.Setup(r => r.GetStopAnnouncementSnapshotAsync(tripId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new StopAnnouncementSnapshot(
                    tripId, Guid.NewGuid(), Guid.NewGuid(), tripType, stations));

            repository.Setup(r => r.GetMaxRouteSegmentCountAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(4);

            repository.Setup(r => r.ListRouteNarrationCandidatesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[]
                {
                    new RouteNarrationCandidate(
                        Guid.NewGuid(), Guid.NewGuid(), "Cầu Ba Son",
                        "Cây cầu dây văng nổi bật trên sông Sài Gòn.",
                        10.7816000m, 106.7112000m, 1, null, null, null),
                    new RouteNarrationCandidate(
                        Guid.NewGuid(), Guid.NewGuid(), "Cột cờ Thủ Ngữ",
                        "Di tích lịch sử lâu đời.",
                        10.7688000m, 106.7072000m, 2, null, null, null),
                });

            var guard = new InMemoryStopAnnouncementEmissionGuard();

            var service = new StopAnnouncementService(
                repository.Object,
                publisher,
                guard,
                new NtsGeoDistanceCalculator(),
                Mock.Of<IDateTimeService>(),
                new StopAnnouncementConfigStore(options),
                NullLogger<StopAnnouncementService>.Instance);

            return new StopAnnouncementFixture { TripId = tripId, Publisher = publisher, Service = service };
        }
    }
}
