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

    [Theory]
    [InlineData(0, false)]
    [InlineData(604800, true)]
    [InlineData(604801, false)]
    public void RangeBoundary_ShouldRetainSevenDayLimit(int seconds, bool valid) =>
        _validator.Validate(new GetBoatPositionHistoryRequest(Guid.NewGuid(), Now, Now.AddSeconds(seconds)))
            .IsValid.Should().Be(valid);

    [Theory]
    [InlineData(0, 100)]
    [InlineData(1, 501)]
    public void InvalidPaging_ShouldStillFail(int page, int size) =>
        _validator.Validate(new GetBoatPositionHistoryRequest(Guid.NewGuid(), Now.AddHours(-1), Now, page, size))
            .IsValid.Should().BeFalse();
}
