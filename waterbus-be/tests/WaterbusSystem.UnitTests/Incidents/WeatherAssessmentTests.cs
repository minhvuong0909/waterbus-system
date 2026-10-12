using FluentAssertions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Incidents;
using Xunit;

namespace WaterbusSystem.UnitTests.Incidents;

public class WeatherAssessmentTests
{
    private static readonly WeatherOptions Options = new() { WindSpeedThresholdMs = 10.6, GustThresholdMs = 17.2 };

    private static string? Assess(double wind, double? gust = null, int code = 800) =>
        WeatherAssessment.Severity(new WeatherObservation(wind, gust, code, "", DateTimeOffset.UtcNow), Options);

    [Fact]
    public void BelowThresholds_ShouldBeNull() => Assess(10.5).Should().BeNull();

    [Fact]
    public void WindAtThreshold_ShouldBeMedium() => Assess(10.6).Should().Be("Medium");

    [Fact]
    public void GustAtThreshold_ShouldBeHigh() => Assess(5, gust: 17.2).Should().Be("High");

    [Theory]
    [InlineData(200)]
    [InlineData(211)]
    [InlineData(232)]
    public void Thunderstorm_ShouldBeHighEvenWithLightWind(int code) => Assess(2, code: code).Should().Be("High");

    [Theory]
    [InlineData(300)] // mưa phùn
    [InlineData(500)] // mưa
    [InlineData(800)] // trời quang
    public void NonThunderstormCodes_WithLightWind_ShouldBeNull(int code) => Assess(2, code: code).Should().BeNull();

    [Fact]
    public void Describe_ShouldMentionRouteAndKmPerHour() =>
        WeatherAssessment.Describe("RT01", new WeatherObservation(10, 20, 211, "dông", DateTimeOffset.UtcNow))
            .Should().Contain("RT01").And.Contain("36 km/h").And.Contain("72 km/h");
}
