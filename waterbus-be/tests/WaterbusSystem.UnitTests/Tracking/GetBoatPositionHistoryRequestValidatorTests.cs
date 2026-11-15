using FluentAssertions;
using WaterbusSystem.Application.DTOs.Requests.Tracking;
using WaterbusSystem.Application.Validators.Tracking;
using Xunit;

namespace WaterbusSystem.UnitTests.Tracking;

public class GetBoatPositionHistoryRequestValidatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 3, 0, 0, TimeSpan.Zero);
    private readonly GetBoatPositionHistoryRequestValidator _validator = new();

    [Fact]
    public void ValidRequest_ShouldPass() =>
        _validator.Validate(new GetBoatPositionHistoryRequest(Guid.NewGuid(), Now.AddHours(-1), Now)).IsValid.Should().BeTrue();

    [Fact]
    public void EmptyBoatId_ShouldFail() =>
        _validator.Validate(new GetBoatPositionHistoryRequest(Guid.Empty, Now.AddHours(-1), Now))
            .Errors.Should().Contain(e => e.PropertyName == "BoatId");

    [Fact]
    public void RangeLongerThanSevenDays_ShouldFail() =>
        _validator.Validate(new GetBoatPositionHistoryRequest(Guid.NewGuid(), Now.AddDays(-8), Now)).IsValid.Should().BeFalse();
}
