using FluentAssertions;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.DTOs.Requests.Bookings;
using WaterbusSystem.Application.Features.Bookings.Services;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Rules;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;

namespace WaterbusSystem.UnitTests.Bookings;

/// <summary>
/// BR-ROUTE-06: chuyến Sightseeing dạng vòng được lên và xuống cùng một Station; chuyến thường thì không.
/// Cùng một quy tắc cho đặt vé trực tiếp và giữ chỗ checkout (cùng đi qua ResolveJourneyCalls).
/// </summary>
public class JourneySameStationTests
{
    [Theory]
    [InlineData(TripType.Commuter, false)]
    [InlineData(TripType.Sightseeing, true)]
    public void DomainRule_AllowsSameStationOnlyForSightseeing(TripType type, bool allowed)
    {
        var station = Guid.NewGuid();

        JourneyRules.AllowsSameStation(type).Should().Be(allowed);
        JourneyRules.IsForbiddenSameStation(type, station, station).Should().Be(!allowed);
        JourneyRules.IsForbiddenSameStation(type, station, Guid.NewGuid()).Should().BeFalse(); // hai bến khác nhau luôn được
    }

    /// <summary>Trip A(1), B(2), C(3) trở thành vòng A(1), B(2), C(3), A(4).</summary>
    private static TestDbContextFactory.TripSeedData CircularTrip(ApplicationDbContext context, TripType type)
    {
        var seed = TestDbContextFactory.SeedTrip(context);
        var stop = new RouteStop { RouteId = seed.Route.Id, StationId = seed.StationA.Id, SequenceNo = 4 };
        context.RouteStops.Add(stop);
        context.TripStopCalls.Add(new TripStopCall { TripId = seed.Trip.Id, RouteStop = stop, VisitOrder = 4 });
        seed.Trip.TripType = type;
        seed.Route.ServiceType = type == TripType.Sightseeing ? "Sightseeing" : "Regular";
        context.SaveChanges();
        return seed;
    }

    private static CreateBookingRequest Booking(TestDbContextFactory.TripSeedData s, Guid from, Guid to) =>
        new(s.Trip.Id, [s.Seat1.Id], from, to, "Nguoi mua", "buyer@test.com", "0901234567", null);

    // ---- đặt vé trực tiếp ----

    [Fact]
    public async Task Booking_OnACommuterTrip_RejectsTheSameStationEvenWhenTheRouteRepeatsIt()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var seed = CircularTrip(context, TripType.Commuter);

        var act = () => CommercialHandlerFactory.Booking(context).CreateAsync(Booking(seed, seed.StationA.Id, seed.StationA.Id));

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Values.SelectMany(v => v)
            .Should().Contain(m => m.Contains("hai bến khác nhau"));
        context.PurchaseOrders.Should().BeEmpty();
        context.SeatReservations.Should().BeEmpty();
    }

    [Fact]
    public async Task Booking_OnASightseeingLoop_AllowsBoardingAndLeavingAtTheSameStation()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var seed = CircularTrip(context, TripType.Sightseeing);

        var result = await CommercialHandlerFactory.Booking(context).CreateAsync(Booking(seed, seed.StationA.Id, seed.StationA.Id));

        var booking = context.Bookings.Single(b => b.Id == result.BookingId);
        var calls = context.TripStopCalls.Where(c => c.TripId == seed.Trip.Id).ToDictionary(c => c.Id, c => c.VisitOrder);
        calls[booking.BoardingCallId].Should().Be(1);        // lần lên đầu
        calls[booking.DisembarkingCallId].Should().Be(4);    // lần xuống cuối, VisitOrder khác nhau
    }

    [Fact]
    public async Task Booking_OnACommuterTrip_StillAllowsTwoDifferentStations()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var seed = CircularTrip(context, TripType.Commuter);

        var act = () => CommercialHandlerFactory.Booking(context).CreateAsync(Booking(seed, seed.StationA.Id, seed.StationC.Id));

        await act.Should().NotThrowAsync();
    }

    // ---- giữ chỗ checkout ----

    [Fact]
    public async Task CheckoutHold_OnACommuterTrip_RejectsTheSameStation_AndLeavesNothingBehind()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var seed = CircularTrip(context, TripType.Commuter);
        var checkout = CommercialHandlerFactory.Checkout(context);

        var act = () => checkout.HoldAsync(new CreateCheckoutHoldRequest([
            new CheckoutHoldLegRequest(seed.Trip.Id, [seed.Seat1.Id], seed.StationA.Id, seed.StationA.Id)]));

        await act.Should().ThrowAsync<ValidationException>();
        context.PurchaseOrders.Should().BeEmpty();
        context.SeatReservations.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckoutHold_OnASightseeingLoop_AllowsTheSameStation()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var seed = CircularTrip(context, TripType.Sightseeing);
        var checkout = CommercialHandlerFactory.Checkout(context);

        var hold = await checkout.HoldAsync(new CreateCheckoutHoldRequest([
            new CheckoutHoldLegRequest(seed.Trip.Id, [seed.Seat1.Id], seed.StationA.Id, seed.StationA.Id)]));

        hold.Bookings.Should().ContainSingle();
    }
}
