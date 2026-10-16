using FluentAssertions;
using WaterbusSystem.Application.Features.AiBooking.Models;
using WaterbusSystem.UnitTests.AiBooking.Fakes;
using Xunit;

namespace WaterbusSystem.UnitTests.AiBooking;

public class FakeBookingLinkToolTests
{
    private readonly FakeBookingLinkTool _tool = new();

    [Fact]
    public async Task BuildBookingLinkAsync_ValidCriteria_ReturnsOnlyTestConfiguredUrl()
    {
        var tripId = Guid.NewGuid();
        var depId = Guid.NewGuid();
        var arrId = Guid.NewGuid();

        var criteria = new BookingLinkCriteria
        {
            TripId = tripId,
            BoardingStationId = depId,
            DisembarkingStationId = arrId,
            PassengerCount = 2
        };

        const string testOnlyUrl = "https://booking.example.test/opaque-link";
        var tool = new FakeBookingLinkTool(new BookingLinkResult(testOnlyUrl, true));
        var result = await tool.BuildBookingLinkAsync(criteria);

        result.IsValid.Should().BeTrue();
        result.BookingUrl.Should().Be(testOnlyUrl, "fake chỉ chuyển tiếp link test cấu hình, không tự dựng URL production");
    }

    [Fact]
    public async Task BuildBookingLinkAsync_WithoutOwnerContract_ReturnsNoUrl()
    {
        var result = await _tool.BuildBookingLinkAsync(new BookingLinkCriteria { TripId = Guid.NewGuid() });

        result.IsValid.Should().BeFalse();
        result.BookingUrl.Should().BeNull();
    }

    [Fact]
    public async Task BuildBookingLinkAsync_EmptyTripId_ReturnsInvalid()
    {
        var criteria = new BookingLinkCriteria
        {
            TripId = Guid.Empty
        };

        var result = await _tool.BuildBookingLinkAsync(criteria);

        result.IsValid.Should().BeFalse();
        result.BookingUrl.Should().BeNull();
        result.Message.Should().Contain("không hợp lệ");
    }
}
