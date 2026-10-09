using FluentAssertions;
using WaterbusSystem.Application.Features.AiBooking.Models;
using WaterbusSystem.UnitTests.AiBooking.Fakes;
using Xunit;

namespace WaterbusSystem.UnitTests.AiBooking;

public class FakeTripSearchToolTests
{
    [Fact]
    public async Task SearchTripsAsync_MissingStationsAndDate_ReturnsRequiresClarification()
    {
        var tool = new FakeTripSearchTool();
        var criteria = new TripSearchCriteria(); // Không có bến đi hay bến đến

        var result = await tool.SearchTripsAsync(criteria);

        result.Should().NotBeNull();
        result.RequiresMoreClarification.Should().BeTrue("chưa có bến đi/đến thì phải yêu cầu làm rõ, không tự đoán");
        result.Trips.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchTripsAsync_CompleteCriteria_ReturnsTripCards()
    {
        var tool = new FakeTripSearchTool();
        var criteria = new TripSearchCriteria
        {
            DepartureStationName = "Bạch Đằng",
            ArrivalStationName = "Linh Đông",
            DepartureDate = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).DateTime).AddDays(1)
        };

        var result = await tool.SearchTripsAsync(criteria);

        result.IsSuccess.Should().BeTrue();
        result.RequiresMoreClarification.Should().BeFalse();
        result.Trips.Should().NotBeEmpty();
        result.Trips.First().DepartureStationName.Should().Be("Bến Bạch Đằng");
        result.Trips.First().TripType.Should().Be("Commuter");
        result.Trips.First().IsPriceVerified.Should().BeFalse("chưa nối FareRule nên giá chưa xác thực, không tự bịa");
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task SearchTripsAsync_IncompleteCriteria_AsksForClarification(bool hasDeparture, bool hasArrival, bool hasDate)
    {
        var criteria = new TripSearchCriteria
        {
            DepartureStationName = hasDeparture ? "Bạch Đằng" : null,
            ArrivalStationName = hasArrival ? "Linh Đông" : null,
            DepartureDate = hasDate
                ? DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).DateTime).AddDays(1)
                : null
        };

        var result = await new FakeTripSearchTool().SearchTripsAsync(criteria);

        result.RequiresMoreClarification.Should().BeTrue();
        result.Trips.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchTripsAsync_NoMatchingTrips_ReturnsEmptyWithoutThrowing()
    {
        var tool = new FakeTripSearchTool(simulateEmptyResult: true);
        var criteria = new TripSearchCriteria
        {
            DepartureStationName = "Bến Không Tồn Tại",
            ArrivalStationName = "Linh Đông",
            DepartureDate = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).DateTime).AddDays(1)
        };

        var result = await tool.SearchTripsAsync(criteria);

        result.IsSuccess.Should().BeTrue();
        result.RequiresMoreClarification.Should().BeFalse();
        result.Trips.Should().BeEmpty("không tìm thấy chuyến thì trả rỗng, không tự bịa ra chuyến");
    }
}
