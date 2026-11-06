using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Moq;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Requests.Schedules;
using WaterbusSystem.Application.DTOs.Requests.TripPlanning;
using WaterbusSystem.Application.Features.Schedules.Services;
using WaterbusSystem.Application.Features.TripPlanning.Services;
using WaterbusSystem.Application.Validators.TripPlanning;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.Infrastructure.Repositories;
using WaterbusSystem.UnitTests.TestHelpers;
using WaterbusSystem.WebApi.Controllers;
using Xunit;
using ValidationException = WaterbusSystem.Application.Common.Exceptions.ValidationException;

namespace WaterbusSystem.UnitTests.Routes;

public class TripPlanningServiceTests
{
    // 2026-11-01 là Chủ Nhật; thứ Hai 2026-11-02 là ngày đầu tuần ISO 1.
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly ApplicationDbContext _context = TestDbContextFactory.CreateDbContext();
    private readonly TestDbContextFactory.TripSeedData _seed;
    private readonly Mock<IDateTimeService> _clock = new();
    private readonly List<RouteStop> _stops;

    public TripPlanningServiceTests()
    {
        _clock.Setup(c => c.Now).Returns(() => Now);
        _seed = TestDbContextFactory.SeedTrip(_context);
        _stops = _context.RouteStops.Where(s => s.RouteId == _seed.Route.Id).OrderBy(s => s.SequenceNo).ToList();
        // Trip seed dùng đồng hồ thật; đẩy ra xa để không đụng các ngày test.
        _seed.Trip.DepartureTime = Now.AddYears(1);
        _seed.Trip.ArrivalTime = Now.AddYears(1).AddMinutes(30);
        _context.SaveChanges();
    }

    private TripPlanningService Service() =>
        new(new TripPlanningRepository(_context), new EfUnitOfWork(_context), new DirectCommercialTransactionRunner(), _clock.Object);

    private async Task<Guid> NewSchedule(string days = "1,2,3,4,5", DateOnly? from = null, DateOnly? to = null)
    {
        var svc = new ScheduleAdminService(new ScheduleAdminRepository(_context), new EfUnitOfWork(_context), new DirectCommercialTransactionRunner());
        return await svc.CreateAsync(new CreateScheduleRequest(_seed.Route.Id, "Commuter", "Outbound", new TimeSpan(8, 0, 0), days,
            from ?? new DateOnly(2026, 11, 1), to, [new(_stops[0].Id, null, 0), new(_stops[1].Id, 20, 22), new(_stops[2].Id, 40, null)]));
    }

    private static GenerateTripsRequest Gen(Guid scheduleId, string from = "2026-11-02", string to = "2026-11-08", int? sales = null, int? checkIn = null) =>
        new(scheduleId, DateOnly.Parse(from), DateOnly.Parse(to), sales, checkIn);

    // ---- Sinh Trip ----

    [Fact]
    public async Task Generate_ShouldCreateTripsOnlyOnOperatingDays_WithVietnamTimeAndStopCalls()
    {
        var id = await NewSchedule();

        var result = await Service().GenerateFromScheduleAsync(Gen(id, sales: 15, checkIn: 30));

        result.Created.Should().HaveCount(5); // T2..T6 trong 2..8/11
        result.SkippedNotOperatingDay.Should().Be(2);
        var trip = _context.Trips.Include(t => t.TripStopCalls).Single(t => t.Id == result.Created[0].TripId);
        trip.DepartureTime.Should().Be(new DateTimeOffset(2026, 11, 2, 1, 0, 0, TimeSpan.Zero)); // 08:00 VN = 01:00 UTC
        trip.ArrivalTime.Should().Be(trip.DepartureTime.AddMinutes(40));
        trip.Status.Should().Be(TripStatus.Scheduled);
        trip.BoatId.Should().BeNull();
        trip.ScheduleId.Should().Be(id);
        trip.SalesCloseAt.Should().Be(trip.DepartureTime.AddMinutes(-15));
        var calls = trip.TripStopCalls.OrderBy(c => c.VisitOrder).ToList();
        calls.Select(c => c.RouteStopId).Should().Equal(_stops.Select(s => s.Id));
        calls[1].EstimatedArrivalTime.Should().Be(trip.DepartureTime.AddMinutes(20));
        calls[1].CheckInOpenAt.Should().Be(trip.DepartureTime.AddMinutes(22 - 30));
        calls[1].CheckInCloseAt.Should().Be(trip.DepartureTime.AddMinutes(22));
        calls.All(c => c.Status == "Planned").Should().BeTrue();
    }

    [Fact]
    public async Task Generate_WithoutOptionalMinutes_ShouldLeaveSalesCloseAndCheckInUnset()
    {
        var result = await Service().GenerateFromScheduleAsync(Gen(await NewSchedule()));

        var trip = _context.Trips.Include(t => t.TripStopCalls).Single(t => t.Id == result.Created[0].TripId);
        trip.SalesCloseAt.Should().BeNull();
        trip.TripStopCalls.All(c => c.CheckInOpenAt == null && c.CheckInCloseAt == null).Should().BeTrue();
    }

    [Fact]
    public async Task Generate_ShouldBeIdempotent_AndCountSoftDeletedTripsAsExisting()
    {
        var id = await NewSchedule();
        await Service().GenerateFromScheduleAsync(Gen(id));
        var tripCount = _context.Trips.Count();

        var again = await Service().GenerateFromScheduleAsync(Gen(id));
        again.Created.Should().BeEmpty();
        again.SkippedExisting.Should().Be(5);
        _context.Trips.Count().Should().Be(tripCount);

        _context.Trips.First(t => t.ScheduleId == id).IsDeleted = true;
        _context.SaveChanges();
        (await Service().GenerateFromScheduleAsync(Gen(id))).Created.Should().BeEmpty(); // unique index vẫn tính bản xoá mềm
    }

    [Fact]
    public async Task Generate_ShouldSkipPastDeparturesAndRespectEffectiveRange()
    {
        var id = await NewSchedule(from: new DateOnly(2026, 11, 4), to: new DateOnly(2026, 11, 5));

        var result = await Service().GenerateFromScheduleAsync(Gen(id));
        result.Created.Should().HaveCount(2);

        _clock.Setup(c => c.Now).Returns(new DateTimeOffset(2026, 11, 20, 0, 0, 0, TimeSpan.Zero));
        var later = await Service().GenerateFromScheduleAsync(Gen(await NewSchedule("1,2,3,4,5,6,7", from: new DateOnly(2026, 11, 6)), "2026-11-02", "2026-11-22"));
        later.SkippedPast.Should().BeGreaterThan(0);
        later.Created.Should().OnlyContain(c => c.DepartureTime > new DateTimeOffset(2026, 11, 20, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task Generate_ShouldRefuseInactiveScheduleRouteOrIncompleteTimes()
    {
        var id = await NewSchedule();
        var svc = new ScheduleAdminService(new ScheduleAdminRepository(_context), new EfUnitOfWork(_context), new DirectCommercialTransactionRunner());

        await svc.DeactivateAsync(id);
        var inactiveSchedule = () => Service().GenerateFromScheduleAsync(Gen(id));
        await inactiveSchedule.Should().ThrowAsync<ConflictException>();
        await svc.ActivateAsync(id);

        _context.ScheduleStops.First(s => s.ScheduleId == id && s.VisitOrder == 2).ArrivalOffsetMin = null;
        _context.SaveChanges();
        var incomplete = () => Service().GenerateFromScheduleAsync(Gen(id));
        await incomplete.Should().ThrowAsync<ConflictException>();
        _context.Trips.Count(t => t.ScheduleId == id).Should().Be(0);

        _context.ScheduleStops.First(s => s.ScheduleId == id && s.VisitOrder == 2).ArrivalOffsetMin = 20;
        _seed.Route.IsActive = false;
        _context.SaveChanges();
        var inactiveRoute = () => Service().GenerateFromScheduleAsync(Gen(id));
        await inactiveRoute.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Generate_UnknownSchedule_ShouldBeNotFound()
    {
        var act = () => Service().GenerateFromScheduleAsync(Gen(Guid.NewGuid()));

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public void Generate_Validator_ShouldBoundRangeAndMinutes()
    {
        var v = new GenerateTripsRequestValidator();
        var ok = Gen(Guid.NewGuid());

        v.Validate(ok).IsValid.Should().BeTrue();
        v.Validate(ok with { ToDate = new DateOnly(2026, 12, 20) }).IsValid.Should().BeFalse();
        v.Validate(ok with { ToDate = new DateOnly(2026, 10, 1) }).IsValid.Should().BeFalse();
        v.Validate(ok with { SalesCloseMinutesBeforeDeparture = -1 }).IsValid.Should().BeFalse();
        v.Validate(ok with { CheckInOpenMinutesBeforeDeparture = 0 }).IsValid.Should().BeFalse();
    }

    // ---- Gán / đổi tàu ----

    private Boat NewBoat(int seats = 2, bool active = true, SeatClass? seatClass = null, string prefix = "S")
    {
        var boat = new Boat
        {
            Code = $"B{Guid.NewGuid():N}"[..10], Name = "N", RegistrationNumber = Guid.NewGuid().ToString("N")[..12], TotalSeats = seats,
            Manufacturer = "T", YearBuilt = 2024, IsActive = active
        };
        _context.Boats.Add(boat);
        for (var i = 1; i <= seats; i++)
            _context.Seats.Add(new Seat
            {
                BoatId = boat.Id, SeatClassId = (seatClass ?? _seed.StandardClass).Id, SeatCode = $"{prefix}{i:00}", RowNumber = 1, ColumnNumber = i, IsActive = true
            });
        _context.SaveChanges();
        return boat;
    }

    private SeatReservation Reserve(Seat seat, ReservationStatus status = ReservationStatus.Confirmed)
    {
        var booking = TestDbContextFactory.SeedBooking(_context, _seed, from: 1, to: 2);
        var reservation = new SeatReservation
        {
            Booking = booking, BookingId = booking.Id, TripId = _seed.Trip.Id, SeatId = seat.Id, Status = status,
            BoardingStopOrder = 1, DisembarkingStopOrder = 2, SeatCodeAtSale = seat.SeatCode, SeatClassAtSale = "Tieu chuan"
        };
        _context.SeatReservations.Add(reservation);
        _context.SaveChanges();
        return reservation;
    }

    [Fact]
    public async Task AssignBoat_ShouldAttachBoatToTripWithoutOne()
    {
        _seed.Trip.BoatId = null;
        _context.SaveChanges();
        var boat = NewBoat();

        var result = await Service().AssignBoatAsync(_seed.Trip.Id, new AssignTripBoatRequest(boat.Id));

        result.PreviousBoatId.Should().BeNull();
        _context.Trips.Single(t => t.Id == _seed.Trip.Id).BoatId.Should().Be(boat.Id);
    }

    [Fact]
    public async Task AssignBoat_ShouldRejectInactiveBoatBoatWithoutSeatsUnknownBoatAndNonScheduledTrip()
    {
        var inactive = () => Service().AssignBoatAsync(_seed.Trip.Id, new AssignTripBoatRequest(NewBoat(active: false).Id));
        var noSeats = () => Service().AssignBoatAsync(_seed.Trip.Id, new AssignTripBoatRequest(NewBoat(seats: 0).Id));
        var unknown = () => Service().AssignBoatAsync(_seed.Trip.Id, new AssignTripBoatRequest(Guid.NewGuid()));
        await inactive.Should().ThrowAsync<ValidationException>();
        await noSeats.Should().ThrowAsync<ValidationException>();
        await unknown.Should().ThrowAsync<NotFoundException>();

        _seed.Trip.Status = TripStatus.Boarding;
        _context.SaveChanges();
        var boarding = () => Service().AssignBoatAsync(_seed.Trip.Id, new AssignTripBoatRequest(NewBoat().Id));
        await boarding.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task AssignBoat_ShouldRejectOverlappingTripsOfTheSameBoat_ButIgnoreCancelledAndNonOverlapping()
    {
        var boat = NewBoat();
        Trip Other(DateTimeOffset dep, TripStatus status) => new()
        {
            RouteId = _seed.Route.Id, BoatId = boat.Id, DepartureTime = dep, ArrivalTime = dep.AddMinutes(30), Status = status
        };
        _context.Trips.Add(Other(_seed.Trip.DepartureTime.AddMinutes(10), TripStatus.Scheduled)); // chồng
        _context.SaveChanges();

        var overlap = () => Service().AssignBoatAsync(_seed.Trip.Id, new AssignTripBoatRequest(boat.Id));
        await overlap.Should().ThrowAsync<ConflictException>();

        _context.Trips.RemoveRange(_context.Trips.Where(t => t.BoatId == boat.Id));
        _context.Trips.Add(Other(_seed.Trip.DepartureTime.AddMinutes(10), TripStatus.Cancelled)); // đã huỷ
        _context.Trips.Add(Other(_seed.Trip.ArrivalTime, TripStatus.Scheduled)); // sát nhưng không chồng
        _context.SaveChanges();

        await Service().AssignBoatAsync(_seed.Trip.Id, new AssignTripBoatRequest(boat.Id));
        _context.Trips.Single(t => t.Id == _seed.Trip.Id).BoatId.Should().Be(boat.Id);
    }

    [Fact]
    public async Task AssignBoat_SameBoat_ShouldBeNoOp()
    {
        var result = await Service().AssignBoatAsync(_seed.Trip.Id, new AssignTripBoatRequest(_seed.Boat.Id));

        result.RemappedReservations.Should().Be(0);
    }

    [Fact]
    public async Task AssignBoat_WithSoldSeats_ShouldRemapToSameCodeAndClassSeats()
    {
        var reservation = Reserve(_seed.Seat1);
        var cancelled = Reserve(_seed.Seat2, ReservationStatus.Cancelled);
        var replacement = NewBoat(); // S01, S02 cùng hạng Standard
        var target = _context.Seats.Single(s => s.BoatId == replacement.Id && s.SeatCode == "S01");

        var result = await Service().AssignBoatAsync(_seed.Trip.Id, new AssignTripBoatRequest(replacement.Id));

        result.RemappedReservations.Should().Be(1);
        result.PreviousBoatId.Should().Be(_seed.Boat.Id);
        _context.SeatReservations.Single(r => r.Id == reservation.Id).SeatId.Should().Be(target.Id);
        _context.SeatReservations.Single(r => r.Id == reservation.Id).SeatCodeAtSale.Should().Be("S01"); // snapshot giữ nguyên
        _context.SeatReservations.Single(r => r.Id == cancelled.Id).SeatId.Should().Be(_seed.Seat2.Id); // đã huỷ: không đụng
    }

    [Fact]
    public async Task AssignBoat_WithSoldSeats_ShouldRejectMissingCodeOrDifferentClass_AndChangeNothing()
    {
        var reservation = Reserve(_seed.Seat1);
        var missingCode = NewBoat(prefix: "Z");
        var otherClass = NewBoat(seatClass: _seed.PremiumClass);

        var noCode = () => Service().AssignBoatAsync(_seed.Trip.Id, new AssignTripBoatRequest(missingCode.Id));
        var wrongClass = () => Service().AssignBoatAsync(_seed.Trip.Id, new AssignTripBoatRequest(otherClass.Id));

        await noCode.Should().ThrowAsync<ConflictException>();
        await wrongClass.Should().ThrowAsync<ConflictException>();
        _context.Trips.Single(t => t.Id == _seed.Trip.Id).BoatId.Should().Be(_seed.Boat.Id);
        _context.SeatReservations.Single(r => r.Id == reservation.Id).SeatId.Should().Be(_seed.Seat1.Id);
    }

    [Fact]
    public void Controller_ShouldRequireAdmin()
    {
        typeof(AdminTripsController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>()
            .Should().ContainSingle(a => a.Policy == "AdminOnly");
        typeof(AdminTripsController).GetMethods().SelectMany(m => m.GetCustomAttributes(typeof(AllowAnonymousAttribute), true)).Should().BeEmpty();
    }
}
