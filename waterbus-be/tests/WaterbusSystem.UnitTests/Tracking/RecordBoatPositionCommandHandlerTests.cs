using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Tracking;
using WaterbusSystem.Application.Features.Tracking.Commands.RecordBoatPosition;
using WaterbusSystem.Application.Features.SightseeingAudio;
using WaterbusSystem.Application.Features.SightseeingAudio.Interfaces;
using WaterbusSystem.Application.Features.SightseeingAudio.Models;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.Infrastructure.Services.Tracking;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;

namespace WaterbusSystem.UnitTests.Tracking;

public class RecordBoatPositionCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 8, 0, 0, TimeSpan.Zero);

    private readonly ApplicationDbContext _context = TestDbContextFactory.CreateDbContext();
    private readonly TestDbContextFactory.TripSeedData _seed;
    private readonly InMemoryBoatPositionCache _cache = new();
    private readonly Mock<IBoatTrackingPublisher> _publisher = new();
    private readonly Mock<ITrackingAudioTriggerEvaluator> _audioEvaluator = new();
    private readonly Guid _captainId = Guid.NewGuid();

    public RecordBoatPositionCommandHandlerTests()
    {
        _seed = TestDbContextFactory.SeedTrip(_context);
        _seed.Boat.CaptainUserId = _captainId;
        _context.SaveChanges();
    }

    private RecordBoatPositionCommandHandler CreateHandler(int persistIntervalSeconds = 15, bool audioEnabled = false) =>
        new(_context, _cache, _publisher.Object,
            Options.Create(new TrackingOptions { PersistIntervalSeconds = persistIntervalSeconds }),
            Options.Create(new SightseeingAudioOptions { EnableGpsTriggerEvaluation = audioEnabled }),
            _audioEvaluator.Object, NullLogger<RecordBoatPositionCommandHandler>.Instance);

    private RecordBoatPositionCommand Gps(DateTimeOffset observedAt, Guid? captainId = null, Guid? boatId = null) =>
        new(boatId ?? _seed.Boat.Id, 10.7769m, 106.7009m, 8m, observedAt, BoatPositionSources.Gps, captainId ?? _captainId);

    [Fact]
    public async Task Handle_AssignedCaptainSendsGps_ShouldPersistCacheAndPublish()
    {
        var result = await CreateHandler().Handle(Gps(Now), CancellationToken.None);

        result.Accepted.Should().BeTrue();
        result.Persisted.Should().BeTrue();
        _context.BoatPositionEvents.Should().ContainSingle(e => e.BoatId == _seed.Boat.Id && e.Source == "GPS");
        (await _cache.GetAsync(_seed.Boat.Id))!.ObservedAt.Should().Be(Now);
        _publisher.Verify(p => p.PublishPositionAsync(
            It.Is<BoatPositionMessage>(m => m.BoatId == _seed.Boat.Id && m.Lat == 10.7769m),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_CaptainOfAnotherBoat_ShouldThrowForbiddenAndPublishNothing()
    {
        var act = () => CreateHandler().Handle(Gps(Now, captainId: Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
        _context.BoatPositionEvents.Should().BeEmpty();
        _publisher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_GpsWithoutCaptain_ShouldThrowForbidden()
    {
        var command = Gps(Now) with { ReportedByCaptainId = null };

        var act = () => CreateHandler().Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task Handle_SimulatedSourceWithoutCaptain_ShouldBeAccepted()
    {
        var command = Gps(Now) with { Source = BoatPositionSources.Simulated, ReportedByCaptainId = null };

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.Accepted.Should().BeTrue();
        _context.BoatPositionEvents.Single().Source.Should().Be("Simulated");
    }

    [Fact]
    public async Task Handle_UnknownBoat_ShouldThrowNotFound()
    {
        var act = () => CreateHandler().Handle(Gps(Now, boatId: Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_PointsWithinPersistInterval_ShouldPublishBothButPersistOnce()
    {
        var handler = CreateHandler(persistIntervalSeconds: 15);

        var first = await handler.Handle(Gps(Now), CancellationToken.None);
        var second = await handler.Handle(Gps(Now.AddSeconds(5)), CancellationToken.None);
        var third = await handler.Handle(Gps(Now.AddSeconds(15)), CancellationToken.None);

        first.Persisted.Should().BeTrue();
        second.Persisted.Should().BeFalse();
        second.Accepted.Should().BeTrue();
        third.Persisted.Should().BeTrue();
        _context.BoatPositionEvents.Should().HaveCount(2);
        _publisher.Verify(p => p.PublishPositionAsync(It.IsAny<BoatPositionMessage>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task Handle_PointOlderThanLatest_ShouldBeIgnored()
    {
        var handler = CreateHandler();
        await handler.Handle(Gps(Now), CancellationToken.None);

        var stale = await handler.Handle(Gps(Now.AddSeconds(-3)), CancellationToken.None);

        stale.Accepted.Should().BeFalse();
        stale.Persisted.Should().BeFalse();
        (await _cache.GetAsync(_seed.Boat.Id))!.ObservedAt.Should().Be(Now);
        _publisher.Verify(p => p.PublishPositionAsync(It.IsAny<BoatPositionMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AudioEnabled_ShouldEvaluateAcceptedGpsOnly()
    {
        _seed.Trip.Status = TripStatus.EnRoute;
        _context.SaveChanges();
        var handler = CreateHandler(audioEnabled: true);

        await handler.Handle(Gps(Now), CancellationToken.None);
        await handler.Handle(Gps(Now.AddSeconds(-1)), CancellationToken.None);

        _audioEvaluator.Verify(e => e.EvaluateAsync(
            It.Is<BoatPositionSnapshot>(p => p.TripId == _seed.Trip.Id && p.BoatId == _seed.Boat.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AudioDisabled_ShouldNotEvaluate()
    {
        await CreateHandler().Handle(Gps(Now), CancellationToken.None);

        _audioEvaluator.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(TripStatus.EnRoute, true)]
    [InlineData(TripStatus.Suspended, true)]   // BR-INC-05: Suspended vẫn cung cấp vị trí
    [InlineData(TripStatus.Boarding, true)]
    [InlineData(TripStatus.Scheduled, false)]
    [InlineData(TripStatus.Completed, false)]
    public async Task Handle_ShouldAttachCurrentTripOnlyWhenTripIsTrackable(TripStatus status, bool expectTrip)
    {
        _seed.Trip.Status = status;
        _context.SaveChanges();

        var result = await CreateHandler().Handle(Gps(Now), CancellationToken.None);

        result.TripId.Should().Be(expectTrip ? _seed.Trip.Id : null);
        _context.BoatPositionEvents.Single().TripId.Should().Be(expectTrip ? _seed.Trip.Id : null);
    }
}
