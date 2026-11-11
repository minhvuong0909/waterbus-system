using FluentAssertions;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.Infrastructure.Services.Tracking;
using Xunit;
using GeoPoint = WaterbusSystem.Infrastructure.Services.Tracking.RoutePathInterpolator.GeoPoint;

namespace WaterbusSystem.UnitTests.Tracking;

public class DemoRoutePathsTests
{
    private static readonly GeoPoint[] StationsInOrder = ApplicationDbContextInitializer.DemoStationCoordinates
        .OrderBy(c => c.Code)
        .Select(c => new GeoPoint(c.NewLat, c.NewLng))
        .ToArray();

    [Fact]
    public void Rt01_ShouldStartAtFirstStationAndEndAtLastStation()
    {
        var path = DemoRoutePaths.For("RT01")!;

        RoutePathInterpolator.DistanceKm(path[0], StationsInOrder[0]).Should().BeLessThan(0.01);
        RoutePathInterpolator.DistanceKm(path[^1], StationsInOrder[^1]).Should().BeLessThan(0.01);
    }

    [Fact]
    public void Rt01_ShouldPassEveryStationInOrder()
    {
        var path = DemoRoutePaths.For("RT01")!;

        var indexes = StationsInOrder
            .Select(station => path
                .Select((point, i) => (i, d: RoutePathInterpolator.DistanceKm(point, station)))
                .MinBy(x => x.d))
            .ToList();

        indexes.Should().OnlyContain(x => x.d < 0.01, "each station is a vertex of the river path (within 10 m)");
        indexes.Select(x => x.i).Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();
    }

    [Fact]
    public void Rt01_LengthShouldBeCloseToTheSeededRouteDistance()
    {
        // The seed says 10.8 km; the real river path is a bit longer than the straight lines between stations.
        RoutePathInterpolator.TotalLengthKm(DemoRoutePaths.For("RT01")!).Should().BeInRange(10.5, 12.0);
    }

    [Fact]
    public void Rt01_ShouldBeLongerThanStraightLinesBetweenStations()
    {
        RoutePathInterpolator.TotalLengthKm(DemoRoutePaths.For("RT01")!)
            .Should().BeGreaterThan(RoutePathInterpolator.TotalLengthKm(StationsInOrder));
    }

    [Theory]
    [InlineData("rt01")]
    [InlineData("RT01")]
    public void For_ShouldIgnoreCase(string code) => DemoRoutePaths.For(code).Should().NotBeNull();

    [Theory]
    [InlineData("RT99")]
    [InlineData("")]
    [InlineData(null)]
    public void For_UnknownRoute_ShouldReturnNull(string? code) => DemoRoutePaths.For(code).Should().BeNull();
}
