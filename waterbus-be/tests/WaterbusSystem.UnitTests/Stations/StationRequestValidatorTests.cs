using FluentAssertions;
using WaterbusSystem.Application.DTOs.Requests.Stations;
using WaterbusSystem.Application.Validators.Stations;
using Xunit;

namespace WaterbusSystem.UnitTests.Stations;

/// <summary>Tọa độ bến: chỉ chặn giá trị không tồn tại trên Trái Đất; chưa giới hạn theo địa bàn (BR chưa chốt).</summary>
public class StationRequestValidatorTests
{
    private static readonly CreateStationRequestValidator Create = new();
    private static readonly UpdateStationRequestValidator Update = new();

    private static CreateStationRequest NewCreate(double lat = 10.77, double lng = 106.7) =>
        new("BX01", "Bến Bạch Đằng", "Quận 1", lat, lng, 1);

    private static UpdateStationRequest NewUpdate(double lat = 10.77, double lng = 106.7) =>
        new("Bến Bạch Đằng", "Quận 1", lat, lng, 1, true);

    [Fact]
    public void ValidCoordinates_ShouldPass()
    {
        Create.Validate(NewCreate()).IsValid.Should().BeTrue();
        Update.Validate(NewUpdate()).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(-90)]
    [InlineData(90)]
    [InlineData(0)]
    public void LatitudeAtBoundary_ShouldPass(double lat)
    {
        Create.Validate(NewCreate(lat: lat)).IsValid.Should().BeTrue();
        Update.Validate(NewUpdate(lat: lat)).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(-90.0001)]
    [InlineData(90.0001)]
    [InlineData(1000)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void LatitudeOutOfRange_ShouldFail(double lat)
    {
        Create.Validate(NewCreate(lat: lat)).Errors.Should().Contain(e => e.PropertyName == "Latitude");
        Update.Validate(NewUpdate(lat: lat)).Errors.Should().Contain(e => e.PropertyName == "Latitude");
    }

    [Theory]
    [InlineData(-180)]
    [InlineData(180)]
    public void LongitudeAtBoundary_ShouldPass(double lng)
    {
        Create.Validate(NewCreate(lng: lng)).IsValid.Should().BeTrue();
        Update.Validate(NewUpdate(lng: lng)).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(-180.0001)]
    [InlineData(180.0001)]
    [InlineData(-1000)]
    [InlineData(double.NaN)]
    [InlineData(double.NegativeInfinity)]
    public void LongitudeOutOfRange_ShouldFail(double lng)
    {
        Create.Validate(NewCreate(lng: lng)).Errors.Should().Contain(e => e.PropertyName == "Longitude");
        Update.Validate(NewUpdate(lng: lng)).Errors.Should().Contain(e => e.PropertyName == "Longitude");
    }
}
