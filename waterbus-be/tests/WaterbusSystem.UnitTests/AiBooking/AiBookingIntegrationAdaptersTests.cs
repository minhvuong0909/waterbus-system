using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using WaterbusSystem.Application.DTOs.Requests.Trips;
using WaterbusSystem.Application.DTOs.Responses.Stations;
using WaterbusSystem.Application.DTOs.Responses.Trips;
using WaterbusSystem.Application.Features.AiBooking.Models;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Infrastructure.Services.AiBooking;
using Xunit;

namespace WaterbusSystem.UnitTests.AiBooking;

public sealed class AiBookingIntegrationAdaptersTests
{
    private static readonly Guid DepartureId = Guid.NewGuid();
    private static readonly Guid ArrivalId = Guid.NewGuid();

    private static TripSearchCriteria Criteria() => new()
    {
        DepartureStationName = "Bạch Đằng",
        ArrivalStationName = "Bình An",
        DepartureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
        PassengerCount = 2
    };

    [Fact]
    public async Task SearchTool_ContractNotVerified_DoesNotCallTripSearch()
    {
        var tripService = new Mock<ITripService>();
        var stations = new Mock<IStationService>();
        var tool = new TripSearchTool(stations.Object, tripService.Object, Options.Create(new AiBookingIntegrationOptions()), Environment("Development"));

        var result = await tool.SearchTripsAsync(Criteria());

        result.IsSuccess.Should().BeFalse();
        result.Trips.Should().BeEmpty();
        tripService.VerifyNoOtherCalls();
        stations.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SearchTool_ZeroFare_DoesNotExposeUnverifiedTripCard()
    {
        var tripService = MockTripService(0m);
        var tool = VerifiedSearchTool(tripService.Object, MockStationService().Object);

        var result = await tool.SearchTripsAsync(Criteria());

        result.IsSuccess.Should().BeFalse();
        result.Trips.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchTool_DevelopmentPreview_ExposesZeroFareAndRequestedStationIds()
    {
        var tripService = MockTripService(0m);
        var tool = new TripSearchTool(MockStationService().Object, tripService.Object,
            Options.Create(new AiBookingIntegrationOptions { EnableUnverifiedTripCardsForDevelopment = true }),
            Environment("Development"));

        var result = await tool.SearchTripsAsync(Criteria());

        result.IsSuccess.Should().BeTrue();
        result.Trips.Should().ContainSingle();
        var card = result.Trips[0];
        card.BasePrice.Should().Be(0m);
        card.IsPriceVerified.Should().BeFalse();
        card.IsDevelopmentPreview.Should().BeTrue();
        card.BoardingStationId.Should().Be(DepartureId);
        card.DisembarkingStationId.Should().Be(ArrivalId);
    }

    [Fact]
    public async Task SearchTool_Production_IgnoresDevelopmentPreviewFlag()
    {
        var tripService = MockTripService(0m);
        var stations = new Mock<IStationService>();
        var tool = new TripSearchTool(stations.Object, tripService.Object,
            Options.Create(new AiBookingIntegrationOptions { EnableUnverifiedTripCardsForDevelopment = true }),
            Environment("Production"));

        var result = await tool.SearchTripsAsync(Criteria());

        result.IsSuccess.Should().BeFalse();
        result.Trips.Should().BeEmpty();
        tripService.VerifyNoOtherCalls();
        stations.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SearchTool_VerifiedData_MapsCardWithoutInventingBookingLink()
    {
        var tripService = MockTripService(20000m);
        var tool = VerifiedSearchTool(tripService.Object, MockStationService().Object);

        var result = await tool.SearchTripsAsync(Criteria());

        result.IsSuccess.Should().BeTrue();
        result.Trips.Should().ContainSingle();
        result.Trips[0].BasePrice.Should().Be(20000m);
        result.Trips[0].IsPriceVerified.Should().BeTrue();
        result.Trips[0].BookingUrl.Should().BeNull();
        result.Trips[0].BoardingStationId.Should().Be(DepartureId);
        result.Trips[0].DisembarkingStationId.Should().Be(ArrivalId);
    }

    [Fact]
    public async Task SearchTool_OneStation_SearchesBothDirectionsAndDeduplicates()
    {
        var tripService = MockTripService(20000m);
        var tool = VerifiedSearchTool(tripService.Object, MockStationService().Object);
        var criteria = new TripSearchCriteria { DepartureStationName = "Bạch Đằng" };

        var result = await tool.SearchTripsAsync(criteria);

        result.IsSuccess.Should().BeTrue();
        result.Trips.Should().ContainSingle();
        result.Trips[0].BoardingStationId.Should().Be(DepartureId);
        result.Trips[0].DisembarkingStationId.Should().Be(ArrivalId);
        tripService.Verify(s => s.SearchAsync(It.Is<SearchTripsRequest>(q =>
            q.DepartureStationId == DepartureId && q.ArrivalStationId == null
            && q.DepartureDate == null), It.IsAny<CancellationToken>()), Times.Once);
        tripService.Verify(s => s.SearchAsync(It.Is<SearchTripsRequest>(q =>
            q.DepartureStationId == null && q.ArrivalStationId == DepartureId
            && q.DepartureDate == null), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchTool_NoStation_SearchesAllUpcomingTrips()
    {
        var tripService = MockTripService(20000m);
        var tool = VerifiedSearchTool(tripService.Object, MockStationService().Object);

        var result = await tool.SearchTripsAsync(new TripSearchCriteria());

        result.IsSuccess.Should().BeTrue();
        result.Trips.Should().ContainSingle();
        tripService.Verify(s => s.SearchAsync(It.Is<SearchTripsRequest>(q =>
            q.DepartureStationId == null && q.ArrivalStationId == null
            && q.DepartureDate == null), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchTool_OnlyArrival_ReturnsTripEndingAtStation()
    {
        var tripService = MockTripService(20000m);
        var tool = VerifiedSearchTool(tripService.Object, MockStationService().Object);

        var result = await tool.SearchTripsAsync(new TripSearchCriteria
        {
            ArrivalStationName = "Bình An",
            RestrictSingleStationToRequestedDirection = true
        });

        result.Trips.Should().ContainSingle();
        result.Trips[0].BoardingStationId.Should().Be(DepartureId);
        result.Trips[0].DisembarkingStationId.Should().Be(ArrivalId);
        tripService.Verify(s => s.SearchAsync(It.Is<SearchTripsRequest>(q =>
            q.DepartureStationId == null && q.ArrivalStationId == ArrivalId),
            It.IsAny<CancellationToken>()), Times.Once);
        tripService.Verify(s => s.SearchAsync(It.Is<SearchTripsRequest>(q =>
            q.DepartureStationId == ArrivalId && q.ArrivalStationId == null),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SearchTool_ExplicitDeparture_DoesNotSearchArrivals()
    {
        var tripService = MockTripService(20000m);
        var tool = VerifiedSearchTool(tripService.Object, MockStationService().Object);

        await tool.SearchTripsAsync(new TripSearchCriteria
        {
            DepartureStationName = "Bạch Đằng",
            RestrictSingleStationToRequestedDirection = true
        });

        tripService.Verify(s => s.SearchAsync(It.Is<SearchTripsRequest>(q =>
            q.DepartureStationId == DepartureId && q.ArrivalStationId == null),
            It.IsAny<CancellationToken>()), Times.Once);
        tripService.Verify(s => s.SearchAsync(It.Is<SearchTripsRequest>(q =>
            q.DepartureStationId == null && q.ArrivalStationId == DepartureId),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SearchTool_OneStation_ReturnsBothDeparturesAndArrivals()
    {
        var tripService = MockTripService(20000m);
        var departureTrip = new TripSearchResponse(Guid.NewGuid(), "Bạch Đằng - Bình An", "Bạch Đằng", "Bình An",
            DateTimeOffset.UtcNow.AddHours(2), DateTimeOffset.UtcNow.AddHours(3), 60, "Commuter", 20000m, 12,
            new List<TripSearchStopResponse> { new(DepartureId, "Bạch Đằng", 1), new(ArrivalId, "Bình An", 2) });
        var arrivalTrip = new TripSearchResponse(Guid.NewGuid(), "Bình An - Bạch Đằng", "Bình An", "Bạch Đằng",
            DateTimeOffset.UtcNow.AddHours(4), DateTimeOffset.UtcNow.AddHours(5), 60, "Commuter", 20000m, 12,
            new List<TripSearchStopResponse> { new(ArrivalId, "Bình An", 1), new(DepartureId, "Bạch Đằng", 2) });
        tripService.Setup(s => s.SearchAsync(It.IsAny<SearchTripsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SearchTripsRequest q, CancellationToken _) =>
                q.DepartureStationId == DepartureId ? new List<TripSearchResponse> { departureTrip }
                : q.ArrivalStationId == DepartureId ? new List<TripSearchResponse> { arrivalTrip }
                : new List<TripSearchResponse>());
        var tool = VerifiedSearchTool(tripService.Object, MockStationService().Object);

        var result = await tool.SearchTripsAsync(new TripSearchCriteria { DepartureStationName = "Bạch Đằng" });

        result.Trips.Should().HaveCount(2);
        result.Trips.Select(t => t.TripId).Should().Contain(new[] { departureTrip.Id, arrivalTrip.Id });
        result.Trips[0].BoardingStationId.Should().Be(DepartureId);
        result.Trips[1].DisembarkingStationId.Should().Be(DepartureId);
    }

    [Fact]
    public async Task StationTool_ReturnsActiveStationNames()
    {
        var tripService = MockTripService(20000m);
        var stations = MockStationService();
        var tool = VerifiedSearchTool(tripService.Object, stations.Object);

        var names = await tool.ListActiveStationsAsync();

        names.Should().ContainInOrder("Bến Bạch Đằng", "Bình An");
        stations.Verify(s => s.GetActiveStationsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchTool_OnlyDeparture_HidesPastTrips()
    {
        var tripService = MockTripService(20000m);
        tripService.Setup(s => s.SearchAsync(It.IsAny<SearchTripsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TripSearchResponse>
            {
                new(Guid.NewGuid(), "Tuyến 1", "Bạch Đằng", "Bình An",
                    DateTimeOffset.UtcNow.AddMinutes(-30), DateTimeOffset.UtcNow.AddMinutes(-5),
                    25, "Commuter", 20000m, 12,
                    new List<TripSearchStopResponse>
                    {
                        new(DepartureId, "Bạch Đằng", 1),
                        new(ArrivalId, "Bình An", 2)
                    })
            });
        var tool = VerifiedSearchTool(tripService.Object, MockStationService().Object);

        var result = await tool.SearchTripsAsync(new TripSearchCriteria { DepartureStationName = "Bạch Đằng" });

        result.IsSuccess.Should().BeTrue();
        result.Trips.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchTool_TimeFiltersAndPreference_UseVietnamTimeAndRankNearestFirst()
    {
        var tripService = MockTripService(20000m);
        var date = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).AddDays(2).DateTime);
        var departures = new[] { 15 * 60, 16 * 60 + 5, 16 * 60 + 40, 17 * 60 + 10 };
        var trips = departures.Select(minutes =>
        {
            var localDateTime = date.ToDateTime(TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(minutes)));
            var departure = new DateTimeOffset(localDateTime, TimeSpan.FromHours(7));
            return new TripSearchResponse(Guid.NewGuid(), "Tuyến 1", "Bạch Đằng", "Bình An",
                departure, departure.AddMinutes(30), 30, "Commuter", 20000m, 12,
                new List<TripSearchStopResponse> { new(DepartureId, "Bạch Đằng", 1), new(ArrivalId, "Bình An", 2) });
        }).ToList();
        tripService.Setup(s => s.SearchAsync(It.IsAny<SearchTripsRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(trips);

        var result = await VerifiedSearchTool(tripService.Object, MockStationService().Object).SearchTripsAsync(new TripSearchCriteria
        {
            EarliestDepartureTime = new TimeOnly(16, 0),
            LatestDepartureTime = new TimeOnly(17, 0),
            PreferredDepartureTime = new TimeOnly(16, 30),
            LatestArrivalTime = new TimeOnly(17, 15)
        });

        result.Trips.Should().HaveCount(2);
        result.Trips.Select(t => t.TripId).Should().ContainInOrder(trips[2].Id, trips[1].Id);
    }

    [Fact]
    public async Task SearchTool_FiltersApiPriceAndFreeSeats_WithoutChangingTripSearchQuery()
    {
        var tripService = MockTripService(0m);
        var future = DateTimeOffset.UtcNow.AddDays(1);
        var prices = new[] { 0m, 30000m, 50000m, 70000m };
        var trips = prices.Select((price, index) => new TripSearchResponse(Guid.NewGuid(), "Tuyến test",
            "Bạch Đằng", "Bình An", future.AddHours(index), future.AddHours(index + 1),
            60, "Commuter", price, index == 1 ? 0 : 12,
            new List<TripSearchStopResponse> { new(DepartureId, "Bạch Đằng", 1), new(ArrivalId, "Bình An", 2) })).ToList();
        tripService.Setup(s => s.SearchAsync(It.IsAny<SearchTripsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(trips);
        var tool = new TripSearchTool(MockStationService().Object, tripService.Object,
            Options.Create(new AiBookingIntegrationOptions { EnableUnverifiedTripCardsForDevelopment = true }),
            Environment("Development"));

        var under = await tool.SearchTripsAsync(new TripSearchCriteria { PriceLessThan = 50000m,
            MinimumAvailableSeats = 1 });
        var over = await tool.SearchTripsAsync(new TripSearchCriteria { PriceGreaterThan = 50000m });

        under.Trips.Should().ContainSingle().Which.BasePrice.Should().Be(0m);
        over.Trips.Should().ContainSingle().Which.BasePrice.Should().Be(70000m);
    }

    [Fact]
    public async Task SearchTool_Unfiltered_ReturnsEveryUpcomingTripInsteadOfOnlyTen()
    {
        var tripService = MockTripService(20000m);
        var future = DateTimeOffset.UtcNow.AddDays(1);
        var trips = Enumerable.Range(0, 12).Select(index => new TripSearchResponse(Guid.NewGuid(), "Tuyến test",
            "Bạch Đằng", "Bình An", future.AddMinutes(index), future.AddHours(1).AddMinutes(index),
            60, "Commuter", 20000m, 12,
            new List<TripSearchStopResponse> { new(DepartureId, "Bạch Đằng", 1), new(ArrivalId, "Bình An", 2) })).ToList();
        tripService.Setup(s => s.SearchAsync(It.IsAny<SearchTripsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(trips);

        var result = await VerifiedSearchTool(tripService.Object, MockStationService().Object).SearchTripsAsync(new TripSearchCriteria());

        result.Trips.Should().HaveCount(12);
    }

    [Fact]
    public async Task SearchTool_AreaName_SuggestsStationWithoutGuessing()
    {
        var tripService = MockTripService(20000m);
        var stations = new Mock<IStationService>();
        stations.Setup(s => s.GetActiveStationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StationResponse>
            {
                new(DepartureId, "BD", "Bến Bạch Đằng", "Quận 1", 10, 106, 1, true),
                new(ArrivalId, "LA", "Bến Linh Đông", "Thủ Đức", 10, 106, 2, true)
            });

        var result = await VerifiedSearchTool(tripService.Object, stations.Object).SearchTripsAsync(
            new TripSearchCriteria { ArrivalStationName = "khu vực Thủ Đức" });

        result.RequiresMoreClarification.Should().BeTrue();
        result.Message.Should().Contain("Bến Linh Đông");
        result.Trips.Should().BeEmpty();
        tripService.Verify(s => s.SearchAsync(It.IsAny<SearchTripsRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BookingLinkTool_UnverifiedContract_DoesNotBuildUrl()
    {
        var tool = new ConfiguredBookingLinkTool(Options.Create(new AiBookingIntegrationOptions
        {
            BookingUrlTemplate = "https://waterbus.example/trip?tripId={tripId}"
        }));

        var result = await tool.BuildBookingLinkAsync(new BookingLinkCriteria { TripId = Guid.NewGuid() });

        result.IsValid.Should().BeFalse();
        result.BookingUrl.Should().BeNull();
    }

    [Theory]
    [InlineData("http://waterbus.example/trip?tripId={tripId}")]
    [InlineData("https://waterbus.example/trip")]
    [InlineData("https://waterbus.example/{tripId}/{tripId}")]
    [InlineData("https://user:pass@waterbus.example/trip?tripId={tripId}")]
    public async Task BookingLinkTool_InvalidTemplate_IsRejected(string template)
    {
        var tool = new ConfiguredBookingLinkTool(Options.Create(new AiBookingIntegrationOptions
        {
            IsBookingLinkContractVerified = true,
            BookingUrlTemplate = template
        }));

        var result = await tool.BuildBookingLinkAsync(new BookingLinkCriteria { TripId = Guid.NewGuid() });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task BookingLinkTool_ConfirmedTemplate_UsesExactTripId()
    {
        var tripId = Guid.NewGuid();
        var tool = new ConfiguredBookingLinkTool(Options.Create(new AiBookingIntegrationOptions
        {
            IsBookingLinkContractVerified = true,
            BookingUrlTemplate = "https://waterbus.example/trip?tripId={tripId}"
        }));

        var result = await tool.BuildBookingLinkAsync(new BookingLinkCriteria { TripId = tripId });

        result.IsValid.Should().BeTrue();
        result.BookingUrl.Should().Be($"https://waterbus.example/trip?tripId={tripId:D}");
    }

    [Fact]
    public async Task BookingLinkTool_DoesNotDiscardSelectedStations()
    {
        var tool = new ConfiguredBookingLinkTool(Options.Create(new AiBookingIntegrationOptions
        {
            IsBookingLinkContractVerified = true,
            BookingUrlTemplate = "https://waterbus.example/trip?tripId={tripId}"
        }));

        var result = await tool.BuildBookingLinkAsync(new BookingLinkCriteria
        {
            TripId = Guid.NewGuid(),
            BoardingStationId = DepartureId,
            DisembarkingStationId = ArrivalId
        });

        result.IsValid.Should().BeFalse();
    }

    private static TripSearchTool VerifiedSearchTool(ITripService tripService, IStationService stationService) =>
        new(stationService, tripService, Options.Create(new AiBookingIntegrationOptions { IsTripSearchContractVerified = true }),
            Environment("Production"));

    private static IHostEnvironment Environment(string name)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns(name);
        return environment.Object;
    }

    private static Mock<ITripService> MockTripService(decimal price)
    {
        var tripService = new Mock<ITripService>();
        tripService.Setup(s => s.SearchAsync(It.IsAny<SearchTripsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TripSearchResponse>
            {
                new(Guid.NewGuid(), "Tuyến 1", "Bạch Đằng", "Bình An",
                    DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(1).AddMinutes(25),
                    25, "Commuter", price, 12,
                    new List<TripSearchStopResponse>
                    {
                        new(DepartureId, "Bạch Đằng", 1),
                        new(ArrivalId, "Bình An", 2)
                    })
            });
        return tripService;
    }

    private static Mock<IStationService> MockStationService()
    {
        var stations = new Mock<IStationService>();
        stations.Setup(s => s.GetActiveStationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StationResponse>
            {
                new(DepartureId, "BD", "Bến Bạch Đằng", "", 10, 106, 1, true),
                new(ArrivalId, "BA", "Bình An", "", 10, 106, 2, true)
            });
        return stations;
    }
}
