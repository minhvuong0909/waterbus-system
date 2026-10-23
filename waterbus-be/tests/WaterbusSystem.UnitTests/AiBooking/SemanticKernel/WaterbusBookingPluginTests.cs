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

    [Fact]
    public async Task SearchTripsAsync_NoStations_SearchesUpcomingTrips()
    {
        _tripSearchToolMock.Setup(t => t.SearchTripsAsync(It.IsAny<TripSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TripSearchResult(Array.Empty<AiTripCardDto>(), true));
        var result = await _plugin.SearchTripsAsync();

        result.Should().Contain("Không tìm thấy chuyến tàu nào");
        _plugin.InterceptedCards.Should().BeEmpty();
        _tripSearchToolMock.Verify(t => t.SearchTripsAsync(
            It.Is<TripSearchCriteria>(c => c.DepartureStationName == null
                && c.ArrivalStationName == null && c.DepartureDate == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListStationsAsync_ReturnsOnlyNamesFromStationTool()
    {
        _tripSearchToolMock.Setup(t => t.ListActiveStationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "Bến Bạch Đằng", "Bến Linh Đông" });

        var result = await _plugin.ListStationsAsync();

        result.Should().Contain("Bến Bạch Đằng").And.Contain("Bến Linh Đông");
        _plugin.ListedStations.Should().HaveCount(2);
        _plugin.ExecutedTools.Should().Contain(e => e.ToolName == "ListStations" && e.Success);
    }

    [Fact]
    public async Task SearchTripsAsync_OnlyDeparture_PassesOptionalFiltersToTool()
    {
        _tripSearchToolMock.Setup(t => t.SearchTripsAsync(It.IsAny<TripSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TripSearchResult(Array.Empty<AiTripCardDto>(), true));

        await _plugin.SearchTripsAsync(departureStation: "Bạch Đằng");

        _tripSearchToolMock.Verify(t => t.SearchTripsAsync(
            It.Is<TripSearchCriteria>(c => c.DepartureStationName == "Bạch Đằng"
                && c.ArrivalStationName == null && c.DepartureDate == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchTripsAsync_ContextUpdate_PreservesRouteAndChangesOnlyRequestedCriteria()
    {
        var previous = new TripSearchCriteria
        {
            DepartureStationName = "Bạch Đằng",
            ArrivalStationName = "Linh Đông",
            DepartureDate = new DateOnly(2099, 10, 15),
            PassengerCount = 2,
            PreferredDepartureTime = new TimeOnly(16, 0)
        };
        var plugin = new WaterbusBookingPlugin(_tripSearchToolMock.Object, _bookingLinkToolMock.Object,
            previousSearch: previous);
        _tripSearchToolMock.Setup(t => t.SearchTripsAsync(It.IsAny<TripSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TripSearchResult(Array.Empty<AiTripCardDto>(), true));

        await plugin.SearchTripsAsync(passengerCount: 3, preferredDepartureTime: "15:30", usePreviousSearch: true);

        plugin.LatestCriteria.Should().NotBeNull();
        plugin.LatestCriteria!.DepartureStationName.Should().Be("Bạch Đằng");
        plugin.LatestCriteria.ArrivalStationName.Should().Be("Linh Đông");
        plugin.LatestCriteria.DepartureDate.Should().Be(new DateOnly(2099, 10, 15));
        plugin.LatestCriteria.PassengerCount.Should().Be(3);
        plugin.LatestCriteria.PreferredDepartureTime.Should().Be(new TimeOnly(15, 30));
    }

    [Fact]
    public async Task SearchTripsAsync_PriceAndSeatFilters_AreForwardedWithoutInheritingOldSearch()
    {
        var plugin = new WaterbusBookingPlugin(_tripSearchToolMock.Object, _bookingLinkToolMock.Object,
            previousSearch: new TripSearchCriteria { DepartureStationName = "Bạch Đằng", PassengerCount = 2 });
        _tripSearchToolMock.Setup(t => t.SearchTripsAsync(It.IsAny<TripSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TripSearchResult(Array.Empty<AiTripCardDto>(), true));

        await plugin.SearchTripsAsync(priceLessThan: 50000m, minimumAvailableSeats: 1);

        plugin.LatestCriteria.Should().NotBeNull();
        plugin.LatestCriteria!.DepartureStationName.Should().BeNull();
        plugin.LatestCriteria.PassengerCount.Should().BeNull();
        plugin.LatestCriteria.PriceLessThan.Should().Be(50000m);
        plugin.LatestCriteria.MinimumAvailableSeats.Should().Be(1);
    }

    [Fact]
    public async Task SearchTripsAsync_NegativePrice_DoesNotCallSearchTool()
    {
        var result = await _plugin.SearchTripsAsync(priceGreaterThan: -1m);

        result.Should().Contain("không hợp lệ");
        _tripSearchToolMock.Verify(t => t.SearchTripsAsync(It.IsAny<TripSearchCriteria>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SearchTripsAsync_InvalidTime_DoesNotCallSearchTool()
    {
        var result = await _plugin.SearchTripsAsync(preferredDepartureTime: "4pm");

        result.Should().Contain("Giờ không hợp lệ");
        _tripSearchToolMock.Verify(t => t.SearchTripsAsync(It.IsAny<TripSearchCriteria>(),
            It.IsAny<CancellationToken>()), Times.Never);
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

        result.Should().Contain("Tìm thấy 1 chuyến tàu theo dữ liệu Trip Search");
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

    [Fact]
    public async Task SearchTripsAsync_DevelopmentPreview_KeepsZeroFareButNeverSearchProvidedLink()
    {
        var card = new AiTripCardDto(Guid.NewGuid(), "Tuyến test", "Bến A", "Bến B",
            DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(1).AddMinutes(30),
            30, "Commuter", 0m, false, 5, "https://unverified.example.test/booking",
            Array.Empty<AiTripStopDto>(), Guid.NewGuid(), Guid.NewGuid(), true);
        _tripSearchToolMock.Setup(t => t.SearchTripsAsync(It.IsAny<TripSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TripSearchResult(new[] { card }, true));

        await _plugin.SearchTripsAsync("Bến A", "Bến B", "2099-10-15");

        var displayed = _plugin.InterceptedCards.Single();
        displayed.BasePrice.Should().Be(0m);
        displayed.IsPriceVerified.Should().BeFalse();
        displayed.BookingUrl.Should().BeNull();
        displayed.BoardingStationId.Should().Be(card.BoardingStationId);
        displayed.DisembarkingStationId.Should().Be(card.DisembarkingStationId);
    }
}
