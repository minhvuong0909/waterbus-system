using FluentAssertions;
using WaterbusSystem.Infrastructure.Services.Tracking;
using Xunit;
using GeoPoint = WaterbusSystem.Infrastructure.Services.Tracking.RoutePathInterpolator.GeoPoint;

namespace WaterbusSystem.UnitTests.Tracking;

public class RoutePathInterpolatorTests
{
    // 3 điểm thẳng hàng theo vĩ độ, 2 đoạn dài bằng nhau
    private static readonly List<GeoPoint> Path =
    [
        new(10.70, 106.70),
        new(10.80, 106.70),
        new(10.90, 106.70)
    ];

    [Fact]
    public void PointAt_Start_ShouldBeFirstStop() =>
        RoutePathInterpolator.PointAt(Path, 0).Should().Be(Path[0]);

    [Fact]
    public void PointAt_End_ShouldBeLastStop()
    {
        var p = RoutePathInterpolator.PointAt(Path, 1);
        p.Latitude.Should().BeApproximately(10.90, 1e-9);
        p.Longitude.Should().BeApproximately(106.70, 1e-9);
    }

    [Fact]
    public void PointAt_Half_ShouldBeMiddleStop() =>
        RoutePathInterpolator.PointAt(Path, 0.5).Latitude.Should().BeApproximately(10.80, 1e-6);

    [Fact]
    public void PointAt_Quarter_ShouldBeHalfwayOnFirstSegment() =>
        RoutePathInterpolator.PointAt(Path, 0.25).Latitude.Should().BeApproximately(10.75, 1e-6);

    [Theory]
    [InlineData(-0.5)]
    [InlineData(1.5)]
    public void PointAt_OutOfRangeFraction_ShouldClampToPath(double fraction)
    {
        var p = RoutePathInterpolator.PointAt(Path, fraction);
        p.Latitude.Should().BeInRange(10.70, 10.90);
    }

    [Fact]
    public void PointAt_SinglePoint_ShouldReturnIt() =>
        RoutePathInterpolator.PointAt([Path[0]], 0.7).Should().Be(Path[0]);

    [Fact]
    public void PointAt_EmptyPath_ShouldThrow() =>
        FluentActions.Invoking(() => RoutePathInterpolator.PointAt([], 0.5)).Should().Throw<ArgumentException>();

    [Fact]
    public void DistanceKm_OneTenthDegreeLatitude_ShouldBeAbout11Km() =>
        RoutePathInterpolator.DistanceKm(Path[0], Path[1]).Should().BeApproximately(11.1, 0.1);

    [Fact]
    public void AverageSpeedKnots_ShouldUseTotalLengthOverDuration() =>
        // ~22.24 km trong 1 giờ ≈ 12 hải lý/giờ
        RoutePathInterpolator.AverageSpeedKnots(Path, TimeSpan.FromHours(1)).Should().BeApproximately(12.0, 0.1);
}
