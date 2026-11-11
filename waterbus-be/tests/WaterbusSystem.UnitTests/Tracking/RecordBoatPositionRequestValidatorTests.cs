using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Tracking;
using WaterbusSystem.Application.DTOs.Requests.Tracking;
using WaterbusSystem.Application.Validators.Tracking;
using Xunit;

namespace WaterbusSystem.UnitTests.Tracking;

public class RecordBoatPositionRequestValidatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 8, 0, 0, TimeSpan.Zero);

    private static RecordBoatPositionRequestValidator CreateValidator()
    {
        var clock = new Mock<IDateTimeService>();
        clock.Setup(c => c.Now).Returns(Now);
        return new RecordBoatPositionRequestValidator(clock.Object, Options.Create(new TrackingOptions { MaxPositionAgeMinutes = 60 }));
    }

    private static RecordBoatPositionRequest Valid() =>
        new(Guid.NewGuid(), 10.7769m, 106.7009m, 8m, Now, BoatPositionSources.Gps, Guid.NewGuid());

    [Fact]
    public void ValidRequest_ShouldPass() =>
        CreateValidator().Validate(Valid()).IsValid.Should().BeTrue();

    [Theory]
    [InlineData(90.0001, 0)]
    [InlineData(-90.0001, 0)]
    [InlineData(0, 180.0001)]
    [InlineData(0, -180.0001)]
    public void OutOfRangeCoordinates_ShouldFail(double lat, double lng) =>
        CreateValidator().Validate(Valid() with { Latitude = (decimal)lat, Longitude = (decimal)lng })
            .IsValid.Should().BeFalse();

    [Theory]
    [InlineData(90, 180)]   // biên vẫn hợp lệ
    [InlineData(-90, -180)]
    public void BoundaryCoordinates_ShouldPass(double lat, double lng) =>
        CreateValidator().Validate(Valid() with { Latitude = (decimal)lat, Longitude = (decimal)lng })
            .IsValid.Should().BeTrue();

    [Fact]
    public void FutureObservedAt_ShouldFail() =>
        CreateValidator().Validate(Valid() with { ObservedAt = Now.AddMinutes(5) }).IsValid.Should().BeFalse();

    [Fact]
    public void TooOldObservedAt_ShouldFail() =>
        CreateValidator().Validate(Valid() with { ObservedAt = Now.AddMinutes(-61) }).IsValid.Should().BeFalse();

    [Fact]
    public void NegativeSpeed_ShouldFail() =>
        CreateValidator().Validate(Valid() with { SpeedKnots = -1m }).IsValid.Should().BeFalse();

    [Fact]
    public void UnknownSource_ShouldFail() =>
        CreateValidator().Validate(Valid() with { Source = "Manual" }).IsValid.Should().BeFalse();
}
