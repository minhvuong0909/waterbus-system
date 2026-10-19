using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using WaterbusSystem.Application.Features.AiBooking.Models;
using WaterbusSystem.Application.Features.Stations.Queries.GetStations;
using WaterbusSystem.Application.Features.Trips.Queries.SearchTrips;
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
        var sender = new Mock<ISender>();
        var tool = new MediatRTripSearchTool(sender.Object, Options.Create(new AiBookingIntegrationOptions()), Environment("Development"));

        var result = await tool.SearchTripsAsync(Criteria());

        result.IsSuccess.Should().BeFalse();
        result.Trips.Should().BeEmpty();
        sender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SearchTool_ZeroFare_DoesNotExposeUnverifiedTripCard()
    {
        var sender = MockSender(0m);
        var tool = VerifiedSearchTool(sender.Object);

        var result = await tool.SearchTripsAsync(Criteria());

        result.IsSuccess.Should().BeFalse();
        result.Trips.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchTool_DevelopmentPreview_ExposesZeroFareAndRequestedStationIds()
    {
        var sender = MockSender(0m);
        var tool = new MediatRTripSearchTool(sender.Object,
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
        var sender = MockSender(0m);
        var tool = new MediatRTripSearchTool(sender.Object,
            Options.Create(new AiBookingIntegrationOptions { EnableUnverifiedTripCardsForDevelopment = true }),
            Environment("Production"));

        var result = await tool.SearchTripsAsync(Criteria());

        result.IsSuccess.Should().BeFalse();
        result.Trips.Should().BeEmpty();
        sender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SearchTool_VerifiedData_MapsCardWithoutInventingBookingLink()
    {
        var sender = MockSender(20000m);
        var tool = VerifiedSearchTool(sender.Object);

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

    private static MediatRTripSearchTool VerifiedSearchTool(ISender sender) =>
        new(sender, Options.Create(new AiBookingIntegrationOptions { IsTripSearchContractVerified = true }),
            Environment("Production"));

    private static IHostEnvironment Environment(string name)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns(name);
        return environment.Object;
    }

    private static Mock<ISender> MockSender(decimal price)
    {
        var sender = new Mock<ISender>();
        sender.Setup(s => s.Send(It.IsAny<GetStationsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StationDto>
            {
                new(DepartureId, "BD", "Bạch Đằng", "", 10, 106, 1, true),
                new(ArrivalId, "BA", "Bình An", "", 10, 106, 2, true)
            });
        sender.Setup(s => s.Send(It.IsAny<SearchTripsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TripDto>
            {
                new(Guid.NewGuid(), "Tuyến 1", "Bạch Đằng", "Bình An",
                    DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(1).AddMinutes(25),
                    25, "Commuter", price, 12,
                    new List<TripStopDto>
                    {
                        new(DepartureId, "Bạch Đằng", 1),
                        new(ArrivalId, "Bình An", 2)
                    })
            });
        return sender;
    }
}
