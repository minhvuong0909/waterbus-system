using FluentAssertions;
using WaterbusSystem.Application.DTOs.Requests.Stations;
using WaterbusSystem.Application.Validators.Stations;
using Xunit;

namespace WaterbusSystem.UnitTests.Stations;

/// <summary>Toạ độ bến phải nằm trên Trái Đất. Chưa giới hạn theo địa bàn TP.HCM/Đồng Nai vì BR chưa chốt.</summary>
public class StationRequestValidatorTests
{
    private readonly CreateStationRequestValidator _create = new();
    private readonly UpdateStationRequestValidator _update = new();

    private static CreateStationRequest Create(double lat = 10.77, double lng = 106.70) =>
        new("ST99", "Ben thu", "Dia chi", lat, lng, 1);

    private static UpdateStationRequest Update(double lat = 10.77, double lng = 106.70) =>
        new("Ben thu", "Dia chi", lat, lng, 1, true);

    [Theory]
    [InlineData(10.77, 106.70)]      // TP.HCM
    [InlineData(-90, -180)]          // biên dưới, nhận
    [InlineData(90, 180)]            // biên trên, nhận
    [InlineData(0, 0)]
    [InlineData(21.03, 105.85)]      // ngoài TP.HCM nhưng hợp lệ: chưa giới hạn địa bàn
    public void ValidCoordinates_AreAccepted(double lat, double lng)
    {
        _create.Validate(Create(lat, lng)).IsValid.Should().BeTrue();
        _update.Validate(Update(lat, lng)).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(-90.0001)]
    [InlineData(90.0001)]
    [InlineData(1000)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void LatitudeOutOfRange_IsRejected(double lat)
    {
        _create.Validate(Create(lat: lat)).Errors.Should().Contain(e => e.PropertyName == "Latitude");
        _update.Validate(Update(lat: lat)).Errors.Should().Contain(e => e.PropertyName == "Latitude");
    }

    [Theory]
    [InlineData(-180.0001)]
    [InlineData(180.0001)]
    [InlineData(1000)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void LongitudeOutOfRange_IsRejected(double lng)
    {
        _create.Validate(Create(lng: lng)).Errors.Should().Contain(e => e.PropertyName == "Longitude");
        _update.Validate(Update(lng: lng)).Errors.Should().Contain(e => e.PropertyName == "Longitude");
    }

    [Fact]
    public void OtherRulesAreUnchanged()
    {
        _create.Validate(Create() with { Code = "" }).IsValid.Should().BeFalse();
        _create.Validate(Create() with { OrderIndex = -1 }).IsValid.Should().BeFalse();
        _update.Validate(Update() with { Name = "" }).IsValid.Should().BeFalse();
    }
}
