using FluentAssertions;
using WaterbusSystem.Application.DTOs.Requests.Tracking;
using WaterbusSystem.Application.Validators.Tracking;
using Xunit;

namespace WaterbusSystem.UnitTests.Tracking;

public class GetBoatPositionHistoryRequestValidatorTests
{
    private readonly GetBoatPositionHistoryRequestValidator _sut = new();
    private static readonly DateTimeOffset From = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);

    private static GetBoatPositionHistoryRequest Request(Guid? boat = null, DateTimeOffset? to = null, int page = 1, int size = 100) =>
        new(boat ?? Guid.NewGuid(), From, to ?? From.AddDays(1), page, size);

    [Fact]
    public void ValidRequest_IsAccepted() => _sut.Validate(Request()).IsValid.Should().BeTrue();

    [Fact]
    public void EmptyBoatId_IsRejected() =>
        _sut.Validate(Request(boat: Guid.Empty)).Errors.Should().Contain(e => e.PropertyName == "BoatId");

    [Fact]
    public void ExistingRulesAreUnchanged()
    {
        _sut.Validate(Request(to: From)).IsValid.Should().BeFalse();                 // To phải sau From
        _sut.Validate(Request(to: From.AddDays(7))).IsValid.Should().BeTrue();        // đúng 7 ngày vẫn nhận
        _sut.Validate(Request(to: From.AddDays(7).AddSeconds(1))).IsValid.Should().BeFalse();
        _sut.Validate(Request(page: 0)).IsValid.Should().BeFalse();
        _sut.Validate(Request(size: 501)).IsValid.Should().BeFalse();
    }
}
