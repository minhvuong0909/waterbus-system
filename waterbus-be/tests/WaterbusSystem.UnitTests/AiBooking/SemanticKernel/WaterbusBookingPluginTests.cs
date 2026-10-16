using FluentAssertions;
using Moq;
using WaterbusSystem.Application.Features.AiBooking.Interfaces;
using WaterbusSystem.Application.Features.AiBooking.Models;
using WaterbusSystem.Infrastructure.Services.AiBooking.Plugins;
using Xunit;

namespace WaterbusSystem.UnitTests.AiBooking.SemanticKernel;

/// <summary>
/// Unit tests cho WaterbusBookingPlugin — Semantic Kernel native plugin.
/// Kiểm tra:
/// - Validate tham số đầu vào chặt chẽ.
/// - Thu thập và lưu giữ thẻ chuyến thật (InterceptedCards) từ ITripSearchTool.
/// - Cập nhật link đặt vé an toàn qua IBookingLinkTool mà không tự bịa URL.
/// </summary>
public class WaterbusBookingPluginTests
{
    private readonly Mock<ITripSearchTool> _tripSearchToolMock = new();
    private readonly Mock<IBookingLinkTool> _bookingLinkToolMock = new();
    private readonly WaterbusBookingPlugin _plugin;

    public WaterbusBookingPluginTests()
    {
        _plugin = new WaterbusBookingPlugin(_tripSearchToolMock.Object, _bookingLinkToolMock.Object);
    }

    [Theory]
    [InlineData("", "Linh Đông")]
    [InlineData("Bạch Đằng", "")]
    [InlineData("   ", "Linh Đông")]
    public async Task SearchTripsAsync_MissingStations_ReturnsClarificationMessage(string dep, string arr)
    {
        var result = await _plugin.SearchTripsAsync(dep, arr, "2099-10-15");

        result.Should().Contain("Thiếu thông tin bến khởi hành hoặc bến đến");
        _plugin.InterceptedCards.Should().BeEmpty();
        _tripSearchToolMock.Verify(t => t.SearchTripsAsync(It.IsAny<TripSearchCriteria>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SearchTripsAsync_InvalidDateFormat_ReturnsErrorMessage()
    {
        var result = await _plugin.SearchTripsAsync("Bạch Đằng", "Linh Đông", "ngày-mai");

        result.Should().Contain("Ngày khởi hành không hợp lệ");
        _plugin.InterceptedCards.Should().BeEmpty();
        _tripSearchToolMock.Verify(t => t.SearchTripsAsync(It.IsAny<TripSearchCriteria>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("Regular", 1)]
    [InlineData("Unknown", 1)]
    [InlineData("Commuter", 0)]
    [InlineData("Commuter", 51)]
    public async Task SearchTripsAsync_InvalidToolArguments_DoNotReachSearch(string tripType, int passengerCount)
    {
        var result = await _plugin.SearchTripsAsync("Bạch Đằng", "Linh Đông", "2099-10-15", tripType, passengerCount);

        result.Should().Contain("không hợp lệ");
        _tripSearchToolMock.Verify(t => t.SearchTripsAsync(It.IsAny<TripSearchCriteria>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SearchTripsAsync_ToolCallLimit_BlocksFurtherSearches()
    {
        var plugin = new WaterbusBookingPlugin(_tripSearchToolMock.Object, _bookingLinkToolMock.Object, maxToolCalls: 1);
        _tripSearchToolMock.Setup(t => t.SearchTripsAsync(It.IsAny<TripSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TripSearchResult(Array.Empty<AiTripCardDto>(), true));

        await plugin.SearchTripsAsync("Bạch Đằng", "Linh Đông", "2099-10-15");
        var secondResult = await plugin.SearchTripsAsync("Bạch Đằng", "Linh Đông", "2099-10-16");

        secondResult.Should().Contain("giới hạn gọi công cụ");
        _tripSearchToolMock.Verify(t => t.SearchTripsAsync(It.IsAny<TripSearchCriteria>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchTripsAsync_ValidInput_CallsToolAndInterceptsCards()
    {
        var tripId = Guid.NewGuid();
        var sampleCard = new AiTripCardDto(
            TripId: tripId,
            RouteName: "Bạch Đằng - Linh Đông",
            DepartureStationName: "Bến Bạch Đằng",
            ArrivalStationName: "Bến Linh Đông",
            DepartureTime: DateTimeOffset.UtcNow.AddHours(2),
            ArrivalTime: DateTimeOffset.UtcNow.AddHours(3),
            DurationMinutes: 45,
            TripType: "Commuter",
            BasePrice: null,
            IsPriceVerified: false,
            AvailableSeatsCount: 20,
            BookingUrl: null,
            Stops: new List<AiTripStopDto>());

        _tripSearchToolMock.Setup(t => t.SearchTripsAsync(It.IsAny<TripSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TripSearchResult(
                Trips: new List<AiTripCardDto> { sampleCard },
                IsSuccess: true));

        var result = await _plugin.SearchTripsAsync("Bạch Đằng", "Linh Đông", "2099-10-15", "Commuter", 2);

        result.Should().Contain("Tìm thấy 1 chuyến tàu chính thức");
        _plugin.InterceptedCards.Should().HaveCount(1);
        _plugin.InterceptedCards.First().TripId.Should().Be(tripId);
        _plugin.ExecutedTools.Should().Contain(e => e.ToolName == "SearchTrips" && e.Success);
    }

    [Fact]
    public async Task SearchTripsAsync_EmptySearchResult_DoesNotInterceptAnyCards()
    {
        _tripSearchToolMock.Setup(t => t.SearchTripsAsync(It.IsAny<TripSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TripSearchResult(
                Trips: Array.Empty<AiTripCardDto>(),
                IsSuccess: true));

        var result = await _plugin.SearchTripsAsync("Bạch Đằng", "Linh Đông", "2099-10-15");

        result.Should().Contain("Không tìm thấy chuyến tàu nào");
        _plugin.InterceptedCards.Should().BeEmpty("khi tool trả rỗng thì danh sách thẻ chuyến phải rỗng, không được tự bịa");
    }

    [Fact]
    public async Task BuildBookingLinkAsync_InvalidTripId_ReturnsErrorMessage()
    {
        var result = await _plugin.BuildBookingLinkAsync("invalid-guid");

        result.Should().Contain("Mã chuyến tàu (TripId) không hợp lệ");
        _bookingLinkToolMock.Verify(b => b.BuildBookingLinkAsync(It.IsAny<BookingLinkCriteria>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BuildBookingLinkAsync_ValidTripIdAndReadyContract_EnrichesExistingCard()
    {
        var tripId = Guid.NewGuid();
        var card = new AiTripCardDto(
            TripId: tripId,
            RouteName: "Bạch Đằng - Linh Đông",
            DepartureStationName: "Bến Bạch Đằng",
            ArrivalStationName: "Bến Linh Đông",
            DepartureTime: DateTimeOffset.UtcNow,
            ArrivalTime: DateTimeOffset.UtcNow.AddMinutes(45),
            DurationMinutes: 45,
            TripType: "Commuter",
            BasePrice: null,
            IsPriceVerified: false,
            AvailableSeatsCount: 15,
            BookingUrl: null,
            Stops: new List<AiTripStopDto>());

        _tripSearchToolMock.Setup(t => t.SearchTripsAsync(It.IsAny<TripSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TripSearchResult(new List<AiTripCardDto> { card }, true));

        // Nạp thẻ chuyến vào plugin
        await _plugin.SearchTripsAsync("Bạch Đằng", "Linh Đông", "2099-10-15");

        const string expectedUrl = "https://booking.example.test/opaque-link";
        _bookingLinkToolMock.Setup(b => b.BuildBookingLinkAsync(It.IsAny<BookingLinkCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BookingLinkResult(expectedUrl, true));

        var result = await _plugin.BuildBookingLinkAsync(tripId.ToString());

        result.Should().Contain(expectedUrl);
        _plugin.InterceptedCards.First(c => c.TripId == tripId).BookingUrl.Should().Be(expectedUrl);
    }

    [Fact]
    public async Task BuildBookingLinkAsync_TripNotFromSearch_RejectsBeforeTool()
    {
        var tripId = Guid.NewGuid();
        var result = await _plugin.BuildBookingLinkAsync(tripId.ToString());

        result.Should().Contain("chuyến vừa được công cụ tìm kiếm trả về");
        _bookingLinkToolMock.Verify(b => b.BuildBookingLinkAsync(It.IsAny<BookingLinkCriteria>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SearchTripsAsync_ExistingUrlFromSearch_IsRemoved()
    {
        var card = new AiTripCardDto(Guid.NewGuid(), "Tuyến test", "Bến A", "Bến B",
            DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(1).AddMinutes(30),
            30, "Commuter", 0m, false, 5, "https://unverified.example.test/booking",
            Array.Empty<AiTripStopDto>());
        _tripSearchToolMock.Setup(t => t.SearchTripsAsync(It.IsAny<TripSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TripSearchResult(new[] { card }, true));

        await _plugin.SearchTripsAsync("Bến A", "Bến B", "2099-10-15");

        _plugin.InterceptedCards.Single().BookingUrl.Should().BeNull();
        _plugin.InterceptedCards.Single().BasePrice.Should().BeNull();
    }
}
