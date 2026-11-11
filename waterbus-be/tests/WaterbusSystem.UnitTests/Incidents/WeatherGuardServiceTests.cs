using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Incidents;
using WaterbusSystem.Application.Interfaces.Persistence;
using WaterbusSystem.Application.Features.Incidents.Services;
using WaterbusSystem.Infrastructure.Repositories;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;

namespace WaterbusSystem.UnitTests.Incidents;

public class WeatherGuardServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 8, 0, 0, TimeSpan.Zero);

    private readonly ApplicationDbContext _context = TestDbContextFactory.CreateDbContext();
    private readonly TestDbContextFactory.TripSeedData _seed;
    private readonly Mock<IWeatherProvider> _weather = new();
    private readonly Mock<IOperationsAlertPublisher> _alerts = new();
    private readonly Mock<IDateTimeService> _clock = new();
    private readonly WeatherOptions _options = new() { WindSpeedThresholdMs = 10.6, GustThresholdMs = 17.2, LookaheadHours = 3, AlertWindowHours = 3 };

    public WeatherGuardServiceTests()
    {
        _clock.Setup(c => c.Now).Returns(() => Now);
        _seed = TestDbContextFactory.SeedTrip(_context);
        // Trip khởi hành sau 1 giờ -> nằm trong khung nhìn trước 3 giờ
        _seed.Trip.DepartureTime = Now.AddHours(1);
        _seed.Trip.ArrivalTime = Now.AddHours(1).AddMinutes(30);
        _context.SaveChanges();
    }

    private WeatherGuardService CreateHandler() =>
        new(new TripRepository(_context), new RouteRepository(_context), new IncidentRepository(_context),
            new EfUnitOfWork(_context), new SqlWeatherGuardRunLock(_context), _weather.Object, _alerts.Object, _clock.Object, Options.Create(_options),
            NullLogger<WeatherGuardService>.Instance);

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

        var result = await CreateHandler().RunAsync();

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

        await CreateHandler().RunAsync();

        _context.Trips.Find(_seed.Trip.Id)!.Status.Should().Be(TripStatus.Scheduled);
        _context.Trips.Find(enRoute.Id)!.Status.Should().Be(TripStatus.EnRoute);
        _context.TripOperationEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task RunTwice_ShouldUpdateExistingIncidentInsteadOfDuplicating()
    {
        WeatherIs(wind: 12);
        var handler = CreateHandler();

        await handler.RunAsync();
        var second = await handler.RunAsync();

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
        await handler.RunAsync();

        var laterTrip = AddTrip(Now.AddHours(2));
        WeatherIs(wind: 12, code: 202); // dông -> High
        await handler.RunAsync();

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

        var result = await CreateHandler().RunAsync();

        result.IncidentsCreated.Should().Be(1);
        _context.Incidents.Should().HaveCount(2);
    }

    [Fact]
    public async Task CalmWeather_ShouldCreateNothing()
    {
        WeatherIs(wind: 3);

        var result = await CreateHandler().RunAsync();

        result.RoutesChecked.Should().Be(1);
        _context.Incidents.Should().BeEmpty();
        _alerts.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ProviderFailure_ShouldBeSwallowedAndCounted()
    {
        _weather.Setup(w => w.GetCurrentAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("network down"));

        var result = await CreateHandler().RunAsync();

        result.ObservationFailures.Should().BeGreaterThan(0);
        _context.Incidents.Should().BeEmpty();
    }

    [Fact]
    public async Task NoTripInLookaheadWindow_ShouldNotCallWeatherApi()
    {
        _seed.Trip.DepartureTime = Now.AddHours(5);
        _seed.Trip.ArrivalTime = Now.AddHours(5).AddMinutes(30);
        _context.SaveChanges();

        var result = await CreateHandler().RunAsync();

        result.RoutesChecked.Should().Be(0);
        _weather.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CompletedOrCancelledTrips_ShouldNotBeLinked()
    {
        WeatherIs(wind: 12);
        AddTrip(Now.AddMinutes(30), TripStatus.Cancelled);
        AddTrip(Now.AddMinutes(-10), TripStatus.Completed);

        await CreateHandler().RunAsync();

        _context.IncidentTrips.Select(t => t.TripId).Should().BeEquivalentTo([_seed.Trip.Id]);
    }

    [Fact]
    public async Task StationsWithSameCoordinates_ShouldCallApiOncePerRun()
    {
        WeatherIs(wind: 3); // 3 bến seed cùng toạ độ (0,0)

        await CreateHandler().RunAsync();

        _weather.Verify(w => w.GetCurrentAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private Incident AddOpenWeatherIncident(DateTimeOffset occurredAt)
    {
        var incident = new Incident
        {
            ScopeType = "Route", RouteId = _seed.Route.Id, Source = "WeatherService", IncidentType = "Weather",
            Severity = "Medium", Status = "Open", OccurredAt = occurredAt
        };
        _context.Incidents.Add(incident);
        _context.SaveChanges();
        return incident;
    }

    [Fact]
    public async Task WeatherBackToNormal_ShouldResolveOpenIncident()
    {
        var incident = AddOpenWeatherIncident(Now.AddMinutes(-30));
        WeatherIs(wind: 3);

        var result = await CreateHandler().RunAsync();

        result.IncidentsResolved.Should().Be(1);
        _context.Entry(incident).Reload();
        incident.Status.Should().Be("Resolved");
        incident.ResolvedAt.Should().Be(Now);
        _context.Trips.Find(_seed.Trip.Id)!.Status.Should().Be(TripStatus.Scheduled);
    }

    [Fact]
    public async Task ProviderFailure_ShouldKeepIncidentOpen()
    {
        var incident = AddOpenWeatherIncident(Now.AddMinutes(-30));
        _weather.Setup(w => w.GetCurrentAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("network down"));

        var result = await CreateHandler().RunAsync();

        result.IncidentsResolved.Should().Be(0);
        _context.Entry(incident).Reload();
        incident.Status.Should().Be("Open");
    }

    [Fact]
    public async Task NoTripToWatchAnymore_ShouldResolveOpenIncident()
    {
        var incident = AddOpenWeatherIncident(Now.AddMinutes(-30));
        _seed.Trip.Status = TripStatus.Completed;
        _context.SaveChanges();

        var result = await CreateHandler().RunAsync();

        result.IncidentsResolved.Should().Be(1);
        _context.Entry(incident).Reload();
        incident.Status.Should().Be("Resolved");
        _weather.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task StaleIncident_ShouldBeResolvedWhenReplacedByNewOne()
    {
        var stale = AddOpenWeatherIncident(Now.AddHours(-4));
        WeatherIs(wind: 12);

        await CreateHandler().RunAsync();

        _context.Entry(stale).Reload();
        stale.Status.Should().Be("Resolved");
        _context.Incidents.Count(i => i.Status == "Open").Should().Be(1);
    }

    [Fact]
    public async Task IncidentOfOtherSource_ShouldNeverBeAutoResolved()
    {
        var sos = new Incident
        {
            ScopeType = "Trip", Source = "Captain", IncidentType = "Safety", Severity = "High", Status = "Open", OccurredAt = Now.AddMinutes(-5)
        };
        _context.Incidents.Add(sos);
        _context.SaveChanges();
        WeatherIs(wind: 3);

        await CreateHandler().RunAsync();

        _context.Entry(sos).Reload();
        sos.Status.Should().Be("Open");
    }

    [Fact]
    public async Task RunWhileAnotherRunHoldsTheLock_ShouldThrowConflictAndTouchNothing()
    {
        var busy = new Mock<IWeatherGuardRunLock>();
        busy.Setup(l => l.TryAcquireAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IAsyncDisposable?)null);
        var service = new WeatherGuardService(
            new TripRepository(_context), new RouteRepository(_context), new IncidentRepository(_context),
            new EfUnitOfWork(_context), busy.Object, _weather.Object, _alerts.Object, _clock.Object,
            Options.Create(_options), NullLogger<WeatherGuardService>.Instance);

        var act = () => service.RunAsync();

        await act.Should().ThrowAsync<ConflictException>();
        _weather.VerifyNoOtherCalls();
        _alerts.VerifyNoOtherCalls();
        _context.Incidents.Should().BeEmpty();
    }
}
