using Microsoft.EntityFrameworkCore;
using Moq;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Requests.Trips;
using WaterbusSystem.Application.Features.Trips.Services;
using WaterbusSystem.Application.Validators.Trips;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.Infrastructure.Repositories;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;
using ValidationException = WaterbusSystem.Application.Common.Exceptions.ValidationException;

namespace WaterbusSystem.UnitTests.Trips;

public class TripServiceReadQueriesTests
{
    private sealed class Clock : IDateTimeService { public DateTimeOffset Now { get; } = DateTimeOffset.UtcNow; }
    private readonly Clock _clock = new();

    private static TripService Service(ApplicationDbContext context, IDateTimeService clock) =>
        new(new TripRepository(context), new IncidentRepository(context), new EfUnitOfWork(context),
            Mock.Of<ICurrentUserService>(), clock, Mock.Of<IOperationsAlertPublisher>(), Mock.Of<IUserDirectory>());

    private static GetTripSeatMapRequest Req(TestDbContextFactory.TripSeedData seed, int from = 1, int to = 3) =>
        new(seed.Trip.TripStopCalls.Single(c => c.VisitOrder == from).Id,
            seed.Trip.TripStopCalls.Single(c => c.VisitOrder == to).Id);

    [Fact]
    public async Task DetailExposesActualCallIdsInVisitOrderAndNoPurchaserData()
    {
        using var c = TestDbContextFactory.CreateDbContext(); var seed = TestDbContextFactory.SeedTrip(c);
        seed.StationA.OrderIndex = 90; seed.StationC.OrderIndex = 1; await c.SaveChangesAsync();
        c.ChangeTracker.Clear();
        var dto = await Service(c, _clock).GetDetailAsync(seed.Trip.Id, default);
        Assert.Equal(new[] { 1, 2, 3 }, dto.Stops.Select(s => s.VisitOrder));
        Assert.Equal(seed.StationA.Id, dto.Stops[0].StationId);
        Assert.Equal(seed.Boat.Id, dto.Boat!.BoatId);
        Assert.True(dto.CanBook);
        Assert.Empty(c.ChangeTracker.Entries());
    }

    [Fact]
    public async Task MissingOrSoftDeletedTripIsNotFound()
    {
        using var c = TestDbContextFactory.CreateDbContext(); var seed = TestDbContextFactory.SeedTrip(c);
        var service = Service(c, _clock);
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetDetailAsync(Guid.NewGuid(), default));
        seed.Trip.IsDeleted = true; await c.SaveChangesAsync();
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetDetailAsync(seed.Trip.Id, default));
    }

    [Fact]
    public async Task AvailableMapReturnsRealCoordinatesAndFlatPricesWithoutWritingHolds()
    {
        using var c = TestDbContextFactory.CreateDbContext(); var seed = TestDbContextFactory.SeedTrip(c);
        var service = Service(c, _clock);
        var whole = await service.GetSeatMapAsync(seed.Trip.Id, Req(seed), default);
        var shortLeg = await service.GetSeatMapAsync(seed.Trip.Id, Req(seed, 1, 2), default);
        Assert.Equal(2, whole.AvailableSeatsCount);
        Assert.All(whole.Seats, s => { Assert.Equal("available", s.Status); Assert.Equal(15000m, s.Price); });
        Assert.Equal(seed.Seat1.RowNumber, whole.Seats.Single(s => s.SeatId == seed.Seat1.Id).RowNumber);
        Assert.Equal(whole.Seats.Select(s => s.Price), shortLeg.Seats.Select(s => s.Price));
        Assert.DoesNotContain(whole.Seats, s => s.SeatId == seed.SeatOnOtherBoat.Id);
        Assert.Empty(c.SeatReservations); Assert.Empty(c.PurchaseOrders);
    }

    [Fact]
    public async Task AdjacentSegmentsStayAvailableButOverlapIsHeldWithOrderDeadline()
    {
        using var c = TestDbContextFactory.CreateDbContext(); var seed = TestDbContextFactory.SeedTrip(c);
        var booking = TestDbContextFactory.SeedBooking(c, seed, 1, 2);
        booking.Order!.ExpiresAt = _clock.Now.AddMinutes(4);
        c.SeatReservations.Add(new SeatReservation { Booking = booking, BookingId = booking.Id,
            TripId = seed.Trip.Id, SeatId = seed.Seat1.Id, BoardingStopOrder = 1, DisembarkingStopOrder = 2 });
        await c.SaveChangesAsync(); c.ChangeTracker.Clear();
        var service = Service(c, _clock);
        var adjacent = await service.GetSeatMapAsync(seed.Trip.Id, Req(seed, 2, 3), default);
        Assert.Equal("available", adjacent.Seats.Single(s => s.SeatId == seed.Seat1.Id).Status);
        var overlapping = await service.GetSeatMapAsync(seed.Trip.Id, Req(seed), default);
        var held = overlapping.Seats.Single(s => s.SeatId == seed.Seat1.Id);
        Assert.Equal("held", held.Status); Assert.Equal(booking.Order.ExpiresAt, held.HoldExpiresAt);
    }

    [Theory]
    [InlineData(ReservationStatus.Confirmed, "sold")]
    [InlineData(ReservationStatus.Completed, "sold")]
    [InlineData(ReservationStatus.Cancelled, "available")]
    public async Task ReservationStatusMatchesCheckoutAvailability(ReservationStatus state, string expected)
    {
        using var c = TestDbContextFactory.CreateDbContext(); var seed = TestDbContextFactory.SeedTrip(c);
        var booking = TestDbContextFactory.SeedBooking(c, seed);
        c.SeatReservations.Add(new SeatReservation { Booking = booking, BookingId = booking.Id,
            TripId = seed.Trip.Id, SeatId = seed.Seat1.Id, BoardingStopOrder = 1, DisembarkingStopOrder = 3, Status = state });
        await c.SaveChangesAsync();
        var dto = await Service(c, _clock).GetSeatMapAsync(seed.Trip.Id, Req(seed), default);
        Assert.Equal(expected, dto.Seats.Single(s => s.SeatId == seed.Seat1.Id).Status);
    }

    [Fact]
    public async Task ExpiredPendingReservationRemainsHeldUntilCleanupReleasesIt()
    {
        using var c = TestDbContextFactory.CreateDbContext(); var seed = TestDbContextFactory.SeedTrip(c);
        var booking = TestDbContextFactory.SeedBooking(c, seed);
        booking.Order!.ExpiresAt = _clock.Now.AddSeconds(-1);
        c.SeatReservations.Add(new SeatReservation { Booking = booking, BookingId = booking.Id,
            TripId = seed.Trip.Id, SeatId = seed.Seat1.Id, BoardingStopOrder = 1, DisembarkingStopOrder = 3 });
        await c.SaveChangesAsync();
        var dto = await Service(c, _clock).GetSeatMapAsync(seed.Trip.Id, Req(seed), default);
        Assert.Equal("held", dto.Seats.Single(s => s.SeatId == seed.Seat1.Id).Status);
    }

    [Fact]
    public async Task WrongReversedAndSkippedCallsAreRejected()
    {
        using var c = TestDbContextFactory.CreateDbContext(); var seed = TestDbContextFactory.SeedTrip(c);
        var service = Service(c, _clock);
        await Assert.ThrowsAsync<ValidationException>(() => service.GetSeatMapAsync(seed.Trip.Id, Req(seed) with { BoardingCallId = Guid.NewGuid() }, default));
        await Assert.ThrowsAsync<ValidationException>(() => service.GetSeatMapAsync(seed.Trip.Id, Req(seed, 3, 1), default));
        seed.Trip.TripStopCalls.Single(s => s.VisitOrder == 1).Status = "Skipped"; await c.SaveChangesAsync();
        await Assert.ThrowsAsync<ValidationException>(() => service.GetSeatMapAsync(seed.Trip.Id, Req(seed), default));
    }

    [Fact]
    public async Task SightseeingLoopMayBoardAndAlightAtSamePhysicalStation()
    {
        using var c = TestDbContextFactory.CreateDbContext(); var seed = TestDbContextFactory.SeedTrip(c);
        seed.Trip.TripType = TripType.Sightseeing; seed.Route.ServiceType = "Sightseeing";
        seed.Trip.TripStopCalls.Single(s => s.VisitOrder == 3).RouteStop!.StationId = seed.StationA.Id;
        await c.SaveChangesAsync(); c.ChangeTracker.Clear();
        var dto = await Service(c, _clock).GetSeatMapAsync(seed.Trip.Id, Req(seed), default);
        Assert.Equal(2, dto.AvailableSeatsCount); Assert.All(dto.Seats, s => Assert.Equal(100000m, s.Price));
    }

    [Fact]
    public async Task ClosedSalesAndStartedTripsCannotOfferSeats()
    {
        using var c = TestDbContextFactory.CreateDbContext(); var seed = TestDbContextFactory.SeedTrip(c);
        seed.Trip.SalesCloseAt = _clock.Now; await c.SaveChangesAsync();
        var service = Service(c, _clock);
        var closed = await service.GetSeatMapAsync(seed.Trip.Id, Req(seed), default);
        Assert.False(closed.CanBook); Assert.Equal("salesClosed", closed.SalesState);
        Assert.All(closed.Seats, s => Assert.Equal("unavailable", s.Status));
        seed.Trip.SalesCloseAt = null; seed.Trip.ActualDepartureTime = _clock.Now; await c.SaveChangesAsync();
        Assert.Equal("tripUnavailable", (await service.GetSeatMapAsync(seed.Trip.Id, Req(seed), default)).SalesState);
    }

    [Fact]
    public async Task MissingBoatIsConflictAndMissingFareOrInactiveSeatIsUnselectable()
    {
        using var c = TestDbContextFactory.CreateDbContext(); var seed = TestDbContextFactory.SeedTrip(c);
        var service = Service(c, _clock);
        seed.Seat1.IsActive = false;
        foreach (var fare in c.FareRules) fare.IsActive = false;
        await c.SaveChangesAsync();
        var dto = await service.GetSeatMapAsync(seed.Trip.Id, Req(seed), default);
        Assert.Equal("seatInactive", dto.Seats.Single(s => s.SeatId == seed.Seat1.Id).UnavailableReason);
        Assert.Equal("missingFare", dto.Seats.Single(s => s.SeatId == seed.Seat2.Id).UnavailableReason);
        Assert.False(dto.CanBook);
        seed.Trip.BoatId = null; seed.Trip.Boat = null; await c.SaveChangesAsync();
        await Assert.ThrowsAsync<ConcurrencyException>(() => service.GetSeatMapAsync(seed.Trip.Id, Req(seed), default));
    }

    [Fact]
    public async Task ConflictingEffectiveFaresAreRejectedRatherThanPickingRandomPrice()
    {
        using var c = TestDbContextFactory.CreateDbContext(); var seed = TestDbContextFactory.SeedTrip(c);
        c.FareRules.Add(new FareRule { TripType = "Commuter", SeatClassId = seed.StandardClass.Id,
            Price = 999m, EffectiveFrom = _clock.Now.AddMinutes(-1) }); await c.SaveChangesAsync();
        await Assert.ThrowsAsync<ConcurrencyException>(() => Service(c, _clock).GetSeatMapAsync(seed.Trip.Id, Req(seed), default));
    }

    [Fact]
    public void ValidatorsRequireNonemptyDistinctCallIds()
    {
        Assert.False(new GetTripSeatMapRequestValidator().Validate(new GetTripSeatMapRequest(Guid.Empty, Guid.Empty)).IsValid);
        var id = Guid.NewGuid();
        Assert.False(new GetTripSeatMapRequestValidator().Validate(new GetTripSeatMapRequest(id, id)).IsValid);
    }
}
