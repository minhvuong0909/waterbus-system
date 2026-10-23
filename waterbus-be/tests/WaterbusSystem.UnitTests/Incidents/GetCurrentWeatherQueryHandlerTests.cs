using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Incidents;
using WaterbusSystem.Application.Features.Incidents.Queries.GetCurrentWeather;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;

namespace WaterbusSystem.UnitTests.Incidents;

public class GetCurrentWeatherQueryHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 8, 0, 0, TimeSpan.Zero);

    private readonly ApplicationDbContext _context = TestDbContextFactory.CreateDbContext();
    private readonly TestDbContextFactory.TripSeedData _seed;
    private readonly Mock<IWeatherProvider> _weather = new();
    private readonly WeatherOptions _options = new() { WindSpeedThresholdMs = 10.6, GustThresholdMs = 17.2 };

    public GetCurrentWeatherQueryHandlerTests()
    {
        _seed = TestDbContextFactory.SeedTrip(_context);
    }

    private GetCurrentWeatherQueryHandler CreateHandler() =>
        new(_context, _weather.Object, Options.Create(_options), NullLogger<GetCurrentWeatherQueryHandler>.Instance);

    private void WeatherIs(double wind, double? gust = null, int code = 800, string description = "trời quang") =>
        _weather.Setup(w => w.GetCurrentAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WeatherObservation(wind, gust, code, description, Now));

    [Fact]
    public async Task Handle_CalmWeather_ReturnsNormalRouteWithMeasurementsAndCreatesNoIncident()
    {
        WeatherIs(wind: 3, gust: 5, description: "mây rải rác");

        var result = await CreateHandler().Handle(new GetCurrentWeatherQuery(), CancellationToken.None);

        var route = result.Should().ContainSingle().Subject;
        route.RouteCode.Should().Be(_seed.Route.Code);
        route.Status.Should().Be(RouteWeatherStatus.Normal);
        route.WindSpeedMs.Should().Be(3);
        route.WindGustMs.Should().Be(5);
        route.Description.Should().Be("mây rải rác");
        route.ObservedAt.Should().Be(Now);
        route.PointsFailed.Should().Be(0);
        _context.Incidents.Should().BeEmpty("query chỉ đọc, việc ghi Incident thuộc Weather Guard");
    }

    [Theory]
    [InlineData(12, null, 800, RouteWeatherStatus.Caution)]
    [InlineData(5, 20.0, 800, RouteWeatherStatus.Danger)]
    [InlineData(2, null, 211, RouteWeatherStatus.Danger)]
    [InlineData(10.5, 17.1, 800, RouteWeatherStatus.Normal)]
    public async Task Handle_UsesWeatherGuardThresholds(double wind, double? gust, int code, string expected)
    {
        WeatherIs(wind, gust, code);

        var result = await CreateHandler().Handle(new GetCurrentWeatherQuery(), CancellationToken.None);

        result.Single().Status.Should().Be(expected);
    }

    [Fact]
    public async Task Handle_ProviderFails_ReturnsUnknownInsteadOfThrowing()
    {
        _weather.Setup(w => w.GetCurrentAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("OpenWeatherMap unreachable"));

        var result = await CreateHandler().Handle(new GetCurrentWeatherQuery(), CancellationToken.None);

        var route = result.Single();
        route.Status.Should().Be(RouteWeatherStatus.Unknown);
        route.WindSpeedMs.Should().BeNull();
        route.PointsFailed.Should().Be(route.PointsChecked);
    }

    [Fact]
    public async Task Handle_InactiveRoute_IsNotListed()
    {
        WeatherIs(wind: 3);
        _seed.Route.IsActive = false;
        await _context.SaveChangesAsync();

        var result = await CreateHandler().Handle(new GetCurrentWeatherQuery(), CancellationToken.None);

        result.Should().BeEmpty();
    }
}
