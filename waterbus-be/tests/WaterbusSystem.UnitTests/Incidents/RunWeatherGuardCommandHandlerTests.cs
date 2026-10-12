using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Incidents;
using WaterbusSystem.Application.Features.Incidents.Commands.RunWeatherGuard;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;

namespace WaterbusSystem.UnitTests.Incidents;

public class RunWeatherGuardCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 8, 0, 0, TimeSpan.Zero);

    private readonly ApplicationDbContext _context = TestDbContextFactory.CreateDbContext();
    private readonly TestDbContextFactory.TripSeedData _seed;
    private readonly Mock<IWeatherProvider> _weather = new();
    private readonly Mock<IOperationsAlertPublisher> _alerts = new();
    private readonly Mock<IDateTimeService> _clock = new();
    private readonly WeatherOptions _options = new() { WindSpeedThresholdMs = 10.6, GustThresholdMs = 17.2, LookaheadHours = 3, AlertWindowHours = 3 };

    public RunWeatherGuardCommandHandlerTests()
    {
        _clock.Setup(c => c.Now).Returns(() => Now);
        _seed = TestDbContextFactory.SeedTrip(_context);
        // Trip khởi hành sau 1 giờ -> nằm trong khung nhìn trước 3 giờ
        _seed.Trip.DepartureTime = Now.AddHours(1);
        _seed.Trip.ArrivalTime = Now.AddHours(1).AddMinutes(30);
        _context.SaveChanges();
    }

    private RunWeatherGuardCommandHandler CreateHandler() =>
        new(_context, _weather.Object, _alerts.Object, _clock.Object, Options.Create(_options),
            NullLogger<RunWeatherGuardCommandHandler>.Instance);

    private void WeatherIs(double wind, double? gust = null, int code = 800) =>
        _weather.Setup(w => w.GetCurrentAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WeatherObservation(wind, gust, code, "test", Now));

    private Trip AddTrip(DateTimeOffset departure, TripStatus status = TripStatus.Scheduled)
    {
        var trip = new Trip
        {
            RouteId = _seed.Route.Id, BoatId = _seed.Boat.Id, DepartureTime = departure,
            ArrivalTime = departure.AddMinutes(30), Status = status
        };
        _context.Trips.Add(trip);
        _context.SaveChanges();
        return trip;
    }

    [Fact]
    public async Task StrongWind_ShouldCreateRouteIncidentLinkedToTripsAndPublishAlert()
    {
        WeatherIs(wind: 12);

        var result = await CreateHandler().Handle(new RunWeatherGuardCommand(), CancellationToken.None);

        result.IncidentsCreated.Should().Be(1);
        var incident = _context.Incidents.Single();
        incident.ScopeType.Should().Be("Route");
        incident.RouteId.Should().Be(_seed.Route.Id);
        incident.Source.Should().Be("WeatherService");
        incident.IncidentType.Should().Be("Weather");
        incident.Severity.Should().Be("Medium");
        incident.Status.Should().Be("Open");
        _context.IncidentTrips.Should().ContainSingle(t => t.TripId == _seed.Trip.Id && t.OperationDecision == null);
        _alerts.Verify(a => a.PublishWeatherAlertAsync(
            It.Is<WeatherAlertMessage>(m => m.IncidentId == incident.Id && m.RouteId == _seed.Route.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Incident_ShouldNeverChangeTripStatus()
    {
        WeatherIs(wind: 30, gust: 40, code: 211);
        var enRoute = AddTrip(Now.AddMinutes(-10), TripStatus.EnRoute);

        await CreateHandler().Handle(new RunWeatherGuardCommand(), CancellationToken.None);

        _context.Trips.Find(_seed.Trip.Id)!.Status.Should().Be(TripStatus.Scheduled);
        _context.Trips.Find(enRoute.Id)!.Status.Should().Be(TripStatus.EnRoute);
        _context.TripOperationEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task RunTwice_ShouldUpdateExistingIncidentInsteadOfDuplicating()
    {
        WeatherIs(wind: 12);
        var handler = CreateHandler();

        await handler.Handle(new RunWeatherGuardCommand(), CancellationToken.None);
        var second = await handler.Handle(new RunWeatherGuardCommand(), CancellationToken.None);

        second.IncidentsCreated.Should().Be(0);
        second.IncidentsUpdated.Should().Be(1);
        _context.Incidents.Should().HaveCount(1);
        _context.IncidentTrips.Should().HaveCount(1);
        _alerts.Verify(a => a.PublishWeatherAlertAsync(It.IsAny<WeatherAlertMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SecondRun_ShouldLinkNewlyAffectedTripsAndPublishOnEscalation()
    {
        WeatherIs(wind: 12);
        var handler = CreateHandler();
        await handler.Handle(new RunWeatherGuardCommand(), CancellationToken.None);

        var laterTrip = AddTrip(Now.AddHours(2));
        WeatherIs(wind: 12, code: 202); // dông -> High
        await handler.Handle(new RunWeatherGuardCommand(), CancellationToken.None);

        _context.Incidents.Single().Severity.Should().Be("High");
        _context.IncidentTrips.Select(t => t.TripId).Should().BeEquivalentTo([_seed.Trip.Id, laterTrip.Id]);
        _alerts.Verify(a => a.PublishWeatherAlertAsync(It.IsAny<WeatherAlertMessage>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task OpenIncidentOutsideAlertWindow_ShouldNotBeReused()
    {
        _context.Incidents.Add(new Incident
        {
            ScopeType = "Route", RouteId = _seed.Route.Id, Source = "WeatherService", IncidentType = "Weather",
            Severity = "Medium", Status = "Open", OccurredAt = Now.AddHours(-4)
        });
        _context.SaveChanges();
        WeatherIs(wind: 12);

        var result = await CreateHandler().Handle(new RunWeatherGuardCommand(), CancellationToken.None);

        result.IncidentsCreated.Should().Be(1);
        _context.Incidents.Should().HaveCount(2);
    }

    [Fact]
    public async Task CalmWeather_ShouldCreateNothing()
    {
        WeatherIs(wind: 3);

        var result = await CreateHandler().Handle(new RunWeatherGuardCommand(), CancellationToken.None);

        result.RoutesChecked.Should().Be(1);
        _context.Incidents.Should().BeEmpty();
        _alerts.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ProviderFailure_ShouldBeSwallowedAndCounted()
    {
        _weather.Setup(w => w.GetCurrentAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("network down"));

        var result = await CreateHandler().Handle(new RunWeatherGuardCommand(), CancellationToken.None);

        result.ObservationFailures.Should().BeGreaterThan(0);
        _context.Incidents.Should().BeEmpty();
    }

    [Fact]
    public async Task NoTripInLookaheadWindow_ShouldNotCallWeatherApi()
    {
        _seed.Trip.DepartureTime = Now.AddHours(5);
        _seed.Trip.ArrivalTime = Now.AddHours(5).AddMinutes(30);
        _context.SaveChanges();

        var result = await CreateHandler().Handle(new RunWeatherGuardCommand(), CancellationToken.None);

        result.RoutesChecked.Should().Be(0);
        _weather.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CompletedOrCancelledTrips_ShouldNotBeLinked()
    {
        WeatherIs(wind: 12);
        AddTrip(Now.AddMinutes(30), TripStatus.Cancelled);
        AddTrip(Now.AddMinutes(-10), TripStatus.Completed);

        await CreateHandler().Handle(new RunWeatherGuardCommand(), CancellationToken.None);

        _context.IncidentTrips.Select(t => t.TripId).Should().BeEquivalentTo([_seed.Trip.Id]);
    }

    [Fact]
    public async Task StationsWithSameCoordinates_ShouldCallApiOncePerRun()
    {
        WeatherIs(wind: 3); // 3 bến seed cùng toạ độ (0,0)

        await CreateHandler().Handle(new RunWeatherGuardCommand(), CancellationToken.None);

        _weather.Verify(w => w.GetCurrentAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
