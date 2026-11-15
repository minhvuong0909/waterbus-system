using FluentAssertions;
using Moq;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Requests.Bookings;
using WaterbusSystem.Application.DTOs.Requests.Trips;
using WaterbusSystem.Application.Features.Trips.Services;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Rules;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.Infrastructure.Repositories;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;

namespace WaterbusSystem.UnitTests.Sightseeing;

/// <summary>
/// BR-ROUTE-05/06: Sightseeing chỉ bán trọn hành trình tham quan đã cấu hình (kể cả chuyến vòng A → B → C → A), không cho
/// chọn tùy ý đoạn lên/xuống như Regular. Cùng một quy tắc cho Search, Seat Map, Booking và Checkout Hold.
/// </summary>
public sealed class SightseeingBookingJourneyTests : IDisposable
{
    private readonly ApplicationDbContext _context = TestDbContextFactory.CreateDbContext();
    private readonly TestDbContextFactory.TripSeedData _seed;
    private readonly Mock<IDateTimeService> _clock = new();

    public SightseeingBookingJourneyTests()
    {
        _seed = TestDbContextFactory.SeedTrip(_context);
        _clock.Setup(c => c.Now).Returns(() => DateTimeOffset.UtcNow);
    }

    public void Dispose() => _context.Dispose();

    /// <summary>Chuyến Sightseeing trên Route Sightseeing riêng; <paramref name="stations"/> theo thứ tự ghé, lặp bến được phép.</summary>
    private Trip SeedTour(Station[] stations, Action<List<RouteStop>>? configureStops = null, bool sightseeingRoute = true)
    {
        var route = new Route
        {
            Code = "TOUR" + Guid.NewGuid().ToString("N")[..4], Name = "Tour vong", DepartureStationId = stations[0].Id,
            ArrivalStationId = stations[^1].Id, EstimatedDurationMinutes = 90, DistanceKm = 12, IsActive = true,
            ServiceType = sightseeingRoute ? "Sightseeing" : "Regular"
        };
        _context.Routes.Add(route);
        var stops = stations.Select((s, i) => new RouteStop
        {
            RouteId = route.Id, StationId = s.Id, SequenceNo = i + 1,
            CanBoard = true, CanAlight = true // cấu hình dễ dãi (mặc định của RouteStop): cờ lên/xuống KHÔNG được coi là quyền bán đoạn lẻ
        }).ToList();
        configureStops?.Invoke(stops);
        _context.RouteStops.AddRange(stops);

        var trip = new Trip
        {
            RouteId = route.Id, BoatId = _seed.Boat.Id, TripType = TripType.Sightseeing, Status = TripStatus.Scheduled,
            DepartureTime = DateTimeOffset.UtcNow.AddDays(2), ArrivalTime = DateTimeOffset.UtcNow.AddDays(2).AddMinutes(90),
            SalesCloseAt = DateTimeOffset.UtcNow.AddDays(2).AddMinutes(-15)
        };
        _context.Trips.Add(trip);
        for (var i = 0; i < stops.Count; i++)
            trip.TripStopCalls.Add(new TripStopCall { TripId = trip.Id, RouteStop = stops[i], VisitOrder = i + 1 });
        _context.SaveChanges();
        return trip;
    }

    private Trip Circular() => SeedTour([_seed.StationA, _seed.StationB, _seed.StationC, _seed.StationA]);

    private static TripStopCall Call(Trip trip, int visitOrder) => trip.TripStopCalls.Single(c => c.VisitOrder == visitOrder);

    private TripService Trips() => new(new TripRepository(_context), new IncidentRepository(_context), new EfUnitOfWork(_context),
        Mock.Of<ICurrentUserService>(), _clock.Object, Mock.Of<IOperationsAlertPublisher>(), Mock.Of<IUserDirectory>(),
        TestNotifications.Outbox(_context), new DirectCommercialTransactionRunner());

    private static async Task Rejected(Func<Task> act)
    {
        var error = await Record.ExceptionAsync(act);
        error.Should().BeOfType<ValidationException>(error?.ToString());
    }

    // ---- Domain rule ----

    [Fact]
    public void Domain_CircularTour_AuthorizesFirstAndLastCall_AtTheSameStation()
    {
        var trip = Circular();
        var journey = SightseeingJourney.Resolve(trip);
        journey.IsValid.Should().BeTrue();
        journey.Boarding!.VisitOrder.Should().Be(1);
        journey.Disembarking!.VisitOrder.Should().Be(4);
        journey.Boarding.Id.Should().NotBe(journey.Disembarking.Id);
        journey.Boarding.RouteStop!.StationId.Should().Be(journey.Disembarking.RouteStop!.StationId);
    }

    [Fact]
    public void Domain_LinearTour_AlsoWorks_NotOnlyTheCircularDemo()
    {
        var trip = SeedTour([_seed.StationB, _seed.StationA, _seed.StationC]);
        var journey = SightseeingJourney.Resolve(trip);
        journey.Boarding!.RouteStop!.StationId.Should().Be(_seed.StationB.Id);
        journey.Disembarking!.RouteStop!.StationId.Should().Be(_seed.StationC.Id);
    }

    [Fact]
    public void Domain_RegularTrip_IsNotASightseeingJourney() =>
        SightseeingJourney.Resolve(_seed.Trip).IsValid.Should().BeFalse();

    [Theory]
    [InlineData("skippedFirst")]
    [InlineData("skippedLast")]
    [InlineData("firstCannotBoard")]
    [InlineData("lastCannotAlight")]
    [InlineData("duplicateVisitOrder")]
    [InlineData("singleStop")]
    public void Domain_AmbiguousOrBrokenConfiguration_IsRejectedInsteadOfPickingASegment(string issue)
    {
        var trip = Circular();
        switch (issue)
        {
            case "skippedFirst": Call(trip, 1).Status = "Skipped"; break;
            case "skippedLast": Call(trip, 4).Status = "Skipped"; break;
            case "firstCannotBoard": Call(trip, 1).RouteStop!.CanBoard = false; break;
            case "lastCannotAlight": Call(trip, 4).RouteStop!.CanAlight = false; break;
            case "duplicateVisitOrder": Call(trip, 3).VisitOrder = 2; break;
            case "singleStop": foreach (var c in trip.TripStopCalls.Where(c => c.VisitOrder > 1).ToList()) trip.TripStopCalls.Remove(c); break;
        }
        var journey = SightseeingJourney.Resolve(trip);
        journey.IsValid.Should().BeFalse();
        journey.Error.Should().NotBeNullOrWhiteSpace();
    }

    // ---- Search ----

    [Fact]
    public async Task Search_CircularTour_ShowsTheSameStationAsDepartureAndReturn_WithBackendFare()
    {
        var trip = Circular();
        var found = (await Trips().SearchAsync(new SearchTripsRequest(null, null, null, TripType.Sightseeing), default))
            .Single(r => r.Id == trip.Id);
        found.DepartureStationName.Should().Be(found.ArrivalStationName).And.Be(_seed.StationA.Name);
        found.BasePrice.Should().Be(TestDbContextFactory.SightseeingStandardFare);
        found.TripType.Should().Be("Sightseeing");
    }

    [Fact]
    public async Task Search_AnIntermediateOrPartialPair_DoesNotAdvertiseTheTour()
    {
        var trip = Circular();
        (await Trips().SearchAsync(new SearchTripsRequest(_seed.StationA.Id, _seed.StationB.Id, null, TripType.Sightseeing), default))
            .Should().NotContain(r => r.Id == trip.Id);
        (await Trips().SearchAsync(new SearchTripsRequest(_seed.StationB.Id, _seed.StationC.Id, null, TripType.Sightseeing), default))
            .Should().NotContain(r => r.Id == trip.Id);
        (await Trips().SearchAsync(new SearchTripsRequest(_seed.StationA.Id, _seed.StationA.Id, null, TripType.Sightseeing), default))
            .Should().Contain(r => r.Id == trip.Id);
    }

    [Fact]
    public async Task Search_RouteTypeMismatch_AndBrokenConfiguration_AreNotAdvertised()
    {
        var mismatched = SeedTour([_seed.StationA, _seed.StationB, _seed.StationA], sightseeingRoute: false);
        var broken = Circular();
        Call(broken, 4).RouteStop!.CanAlight = false;
        _context.SaveChanges();
        var found = await Trips().SearchAsync(new SearchTripsRequest(null, null, null, TripType.Sightseeing), default);
        found.Should().NotContain(r => r.Id == mismatched.Id || r.Id == broken.Id);
    }

    [Fact]
    public async Task Search_Regular_Behaviour_IsUnchanged()
    {
        var found = await Trips().SearchAsync(new SearchTripsRequest(_seed.StationA.Id, _seed.StationB.Id, null, TripType.Commuter), default);
        found.Should().ContainSingle(r => r.Id == _seed.Trip.Id);
        (await Trips().SearchAsync(new SearchTripsRequest(_seed.StationA.Id, _seed.StationA.Id, null, TripType.Commuter), default))
            .Should().BeEmpty();
    }

    // ---- Seat map ----

    [Fact]
    public async Task SeatMap_FullTour_IsAvailable_AndPartialTour_IsRejected()
    {
        var trip = Circular();
        var map = await Trips().GetSeatMapAsync(trip.Id, new(Call(trip, 1).Id, Call(trip, 4).Id), default);
        map.Seats.Should().Contain(s => s.Status == "available" && s.Price == TestDbContextFactory.SightseeingStandardFare);
        map.BoardingVisitOrder.Should().Be(1);
        map.DisembarkingVisitOrder.Should().Be(4);

        await Rejected(() => Trips().GetSeatMapAsync(trip.Id, new(Call(trip, 1).Id, Call(trip, 2).Id), default));
        await Rejected(() => Trips().GetSeatMapAsync(trip.Id, new(Call(trip, 2).Id, Call(trip, 4).Id), default));
        await Rejected(() => Trips().GetSeatMapAsync(trip.Id, new(Call(trip, 2).Id, Call(trip, 3).Id), default));
    }

    // ---- Booking creation & checkout hold (direct API requests) ----

    private CreateBookingRequest Book(Trip trip, Guid from, Guid to, Guid? boardingCall = null, Guid? disembarkingCall = null) =>
        new(trip.Id, [_seed.Seat1.Id], from, to, "Buyer", "buyer@example.com", null, null, boardingCall, disembarkingCall);

    [Fact]
    public async Task Booking_CircularFullTour_IsAccepted_WithDistinctCallsAndSightseeingFare()
    {
        var trip = Circular();
        var booking = await CommercialHandlerFactory.Booking(_context)
            .CreateAsync(Book(trip, _seed.StationA.Id, _seed.StationA.Id, Call(trip, 1).Id, Call(trip, 4).Id));
        var stored = _context.Bookings.Single(b => b.Id == booking.BookingId);
        stored.BoardingCallId.Should().Be(Call(trip, 1).Id);
        stored.DisembarkingCallId.Should().Be(Call(trip, 4).Id);
        stored.QuotedTotal.Should().Be(TestDbContextFactory.SightseeingStandardFare);
        _context.Tickets.Should().BeEmpty("Ticket chỉ phát sau khi thanh toán thành công");
    }

    [Fact]
    public async Task Booking_CircularFullTour_WithoutExplicitCalls_ResolvesStationsToFirstAndLastCall()
    {
        var trip = Circular();
        var booking = await CommercialHandlerFactory.Booking(_context).CreateAsync(Book(trip, _seed.StationA.Id, _seed.StationA.Id));
        var stored = _context.Bookings.Single(b => b.Id == booking.BookingId);
        stored.BoardingCallId.Should().Be(Call(trip, 1).Id);
        stored.DisembarkingCallId.Should().Be(Call(trip, 4).Id);
    }

    [Theory]
    [InlineData(1, 2)] // A -> B
    [InlineData(2, 4)] // B -> A
    [InlineData(2, 3)] // B -> C
    [InlineData(1, 3)] // A -> C
    public async Task Booking_PartialSightseeingJourney_IsRejected_AndCreatesNothing(int from, int to)
    {
        var trip = Circular();
        var f = Call(trip, from); var t = Call(trip, to);
        await Rejected(() => CommercialHandlerFactory.Booking(_context).CreateAsync(
            Book(trip, f.RouteStop!.StationId, t.RouteStop!.StationId, f.Id, t.Id)));
        _context.Bookings.Should().BeEmpty();
        _context.SeatReservations.Should().BeEmpty();
        _context.PurchaseOrders.Should().BeEmpty();
    }

    [Fact]
    public async Task Booking_PartialJourneyByStationsOnly_IsRejected()
    {
        var trip = Circular();
        await Rejected(() => CommercialHandlerFactory.Booking(_context).CreateAsync(Book(trip, _seed.StationA.Id, _seed.StationB.Id)));
    }

    [Fact]
    public async Task Booking_ExplicitCallOfAnotherTrip_OrMismatchingStation_IsRejected()
    {
        var trip = Circular();
        var other = Circular();
        await Rejected(() => CommercialHandlerFactory.Booking(_context).CreateAsync(
            Book(trip, _seed.StationA.Id, _seed.StationA.Id, Call(other, 1).Id, Call(trip, 4).Id)));
        await Rejected(() => CommercialHandlerFactory.Booking(_context).CreateAsync(
            Book(trip, _seed.StationB.Id, _seed.StationA.Id, Call(trip, 1).Id, Call(trip, 4).Id)));
    }

    [Fact]
    public async Task Booking_ReversedCalls_AreRejected()
    {
        var trip = Circular();
        await Rejected(() => CommercialHandlerFactory.Booking(_context).CreateAsync(
            Book(trip, _seed.StationA.Id, _seed.StationA.Id, Call(trip, 4).Id, Call(trip, 1).Id)));
    }

    [Fact]
    public async Task Booking_RouteTripTypeMismatch_IsRejected()
    {
        var trip = SeedTour([_seed.StationA, _seed.StationB, _seed.StationA], sightseeingRoute: false);
        await Rejected(() => CommercialHandlerFactory.Booking(_context).CreateAsync(
            Book(trip, _seed.StationA.Id, _seed.StationA.Id)));
    }

    [Fact]
    public async Task CheckoutHold_FullTour_HoldsTheWholeInterval_AndPartialTour_IsRejected()
    {
        var trip = Circular();
        var checkout = CommercialHandlerFactory.Checkout(_context);
        await Rejected(() => checkout.HoldAsync(new([new(trip.Id, [_seed.Seat1.Id], _seed.StationA.Id, _seed.StationB.Id,
            Call(trip, 1).Id, Call(trip, 2).Id)])));
        _context.SeatReservations.Should().BeEmpty();

        await checkout.HoldAsync(new([new(trip.Id, [_seed.Seat1.Id], _seed.StationA.Id, _seed.StationA.Id,
            Call(trip, 1).Id, Call(trip, 4).Id)]));
        var reservation = _context.SeatReservations.Single();
        (reservation.BoardingStopOrder, reservation.DisembarkingStopOrder).Should().Be((1, 4));
    }

    [Fact]
    public async Task CheckoutHold_SecondCustomerCannotTakeTheSameSeat_OnAnOverlappingTour()
    {
        var trip = Circular();
        var first = CommercialHandlerFactory.Checkout(_context);
        var request = new CreateCheckoutHoldRequest([new(trip.Id, [_seed.Seat1.Id], _seed.StationA.Id, _seed.StationA.Id,
            Call(trip, 1).Id, Call(trip, 4).Id)]);
        await first.HoldAsync(request);
        var error = await Record.ExceptionAsync(() => CommercialHandlerFactory.Checkout(_context).HoldAsync(request));
        error.Should().NotBeNull();
        _context.SeatReservations.Count(r => r.SeatId == _seed.Seat1.Id).Should().Be(1);
    }

    [Fact]
    public async Task Booking_RegularJourney_IsUnchanged()
    {
        await CommercialHandlerFactory.Booking(_context).CreateAsync(Book(_seed.Trip, _seed.StationA.Id, _seed.StationC.Id));
        _context.SeatReservations.RemoveRange(_context.SeatReservations);
        _context.Bookings.RemoveRange(_context.Bookings);
        _context.PurchaseOrders.RemoveRange(_context.PurchaseOrders);
        _context.SaveChanges();
        await CommercialHandlerFactory.Booking(_context).CreateAsync(Book(_seed.Trip, _seed.StationB.Id, _seed.StationC.Id)); // trung gian: hợp lệ
        await Rejected(() => CommercialHandlerFactory.Booking(_context).CreateAsync(Book(_seed.Trip, _seed.StationA.Id, _seed.StationA.Id)));
    }
}
