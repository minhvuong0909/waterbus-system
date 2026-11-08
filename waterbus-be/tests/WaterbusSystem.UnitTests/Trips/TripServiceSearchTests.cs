using FluentAssertions;
using Moq;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Requests.Trips;
using WaterbusSystem.Application.DTOs.Responses.Trips;
using WaterbusSystem.Application.Features.Trips.Services;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.Infrastructure.Repositories;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;

namespace WaterbusSystem.UnitTests.Trips;

public class TripServiceSearchTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 5, 0, 0, TimeSpan.Zero);

    private readonly ApplicationDbContext _context = TestDbContextFactory.CreateDbContext();
    private readonly TestDbContextFactory.TripSeedData _seed;
    private readonly Mock<IDateTimeService> _clock = new();

    public TripServiceSearchTests()
    {
        _clock.Setup(c => c.Now).Returns(() => Now);
        _seed = TestDbContextFactory.SeedTrip(_context);
        _seed.Trip.DepartureTime = Now.AddDays(5); // tránh trùng các ngày dùng trong test
        _seed.Trip.ArrivalTime = Now.AddDays(5).AddMinutes(45);
        // Seed dùng đồng hồ thật; test dùng đồng hồ cố định nên đưa hiệu lực giá về trước Now.
        foreach (var fare in _context.FareRules) fare.EffectiveFrom = Now.AddDays(-30);
        _context.SaveChanges();
    }

    private TripService Service() => new(new TripRepository(_context), new IncidentRepository(_context), new EfUnitOfWork(_context),
        Mock.Of<ICurrentUserService>(), _clock.Object, Mock.Of<IOperationsAlertPublisher>(), Mock.Of<IUserDirectory>(),
        TestNotifications.Outbox(_context), new DirectCommercialTransactionRunner());

    private Trip SetDeparture(DateTimeOffset departure)
    {
        _seed.Trip.DepartureTime = departure;
        _seed.Trip.ArrivalTime = departure.AddMinutes(45);
        _seed.Trip.TripType = TripType.Commuter;
        _context.SaveChanges();
        return _seed.Trip;
    }

    private Task<List<TripSearchResponse>> Search(DateTime date) =>
        Service().SearchAsync(new SearchTripsRequest(null, null, date, null), CancellationToken.None);

    // ---- Ngày tìm kiếm là ngày giờ Việt Nam ----

    [Fact]
    public async Task EarlyMorningVietnamTrip_ShouldBeFoundOnItsLocalDate()
    {
        // 06:00 giờ VN ngày 22/10 = 23:00 UTC ngày 21/10
        SetDeparture(new DateTimeOffset(2026, 10, 21, 23, 0, 0, TimeSpan.Zero));

        (await Search(new DateTime(2026, 10, 22))).Should().ContainSingle();
        (await Search(new DateTime(2026, 10, 21))).Should().BeEmpty();
    }

    [Fact]
    public async Task LateEveningUtcTripThatIsNextDayInVietnam_ShouldNotShowOnUtcDate()
    {
        // 18:00 UTC ngày 22/10 = 01:00 giờ VN ngày 23/10
        SetDeparture(new DateTimeOffset(2026, 10, 22, 18, 0, 0, TimeSpan.Zero));

        (await Search(new DateTime(2026, 10, 23))).Should().ContainSingle();
        (await Search(new DateTime(2026, 10, 22))).Should().BeEmpty();
    }

    [Theory]
    [InlineData(0, 0, true)]    // 07:00 VN ngày 22/10
    [InlineData(16, 59, true)]  // 23:59 VN ngày 22/10, phút cuối của ngày
    [InlineData(17, 0, false)]  // 00:00 VN ngày 23/10
    public async Task DayBoundaries_ShouldBeMidnightInVietnam(int utcHour, int utcMinute, bool expected)
    {
        // Ngày 22/10 giờ VN kéo dài từ 21/10 17:00 UTC đến 22/10 17:00 UTC (loại trừ). Các mốc dưới đây
        // đều thuộc ngày 22/10 theo giờ UTC, riêng 17:00 UTC đã là 00:00 giờ VN ngày 23/10.
        SetDeparture(new DateTimeOffset(2026, 10, 22, utcHour, utcMinute, 0, TimeSpan.Zero));

        var found = await Search(new DateTime(2026, 10, 22));

        (found.Count == 1).Should().Be(expected);
    }

    // ---- Giá thẻ chuyến theo bảng giá hiệu lực ----

    private void ReplaceFares(params FareRule[] fares)
    {
        _context.FareRules.RemoveRange(_context.FareRules);
        _context.FareRules.AddRange(fares);
        _context.SaveChanges();
    }

    private FareRule Fare(string tripType, decimal price, bool active = true, DateTimeOffset? from = null, DateTimeOffset? to = null,
        SeatClass? seatClass = null) => new()
    {
        TripType = tripType, SeatClassId = (seatClass ?? _seed.StandardClass).Id, Price = price, IsActive = active,
        EffectiveFrom = from ?? Now.AddDays(-30), EffectiveTo = to
    };

    private Seat AddSeat(SeatClass seatClass, string code, bool active = true)
    {
        var seat = new Seat { BoatId = _seed.Boat.Id, SeatClassId = seatClass.Id, SeatCode = code, RowNumber = 9, ColumnNumber = 9, IsActive = active };
        _context.Seats.Add(seat);
        _context.SaveChanges();
        return seat;
    }

    private Task<List<TripSearchResponse>> SearchAll() =>
        Service().SearchAsync(new SearchTripsRequest(null, null, null, null), CancellationToken.None);

    [Fact]
    public async Task BasePrice_ShouldBeLowestFareAmongSaleableSeats()
    {
        AddSeat(_seed.PremiumClass, "P01");
        ReplaceFares(Fare("Commuter", 20000, seatClass: _seed.PremiumClass), Fare("Commuter", 15000));
        SetDeparture(new DateTimeOffset(2026, 10, 22, 3, 0, 0, TimeSpan.Zero));

        (await Search(new DateTime(2026, 10, 22))).Single().BasePrice.Should().Be(15000);
    }

    [Fact]
    public async Task BasePrice_ShouldIgnoreInactiveExpiredAndFutureFares()
    {
        ReplaceFares(
            Fare("Commuter", 1000, active: false),
            Fare("Commuter", 2000, to: Now.AddDays(-1)),
            Fare("Commuter", 3000, from: Now.AddDays(1)),
            Fare("Commuter", 17000));
        SetDeparture(new DateTimeOffset(2026, 10, 22, 3, 0, 0, TimeSpan.Zero));

        (await Search(new DateTime(2026, 10, 22))).Single().BasePrice.Should().Be(17000);
    }

    [Fact]
    public async Task NoValidFare_ShouldNotAdvertiseTheTripAtZeroPrice()
    {
        ReplaceFares(Fare("Sightseeing", 100000));
        SetDeparture(new DateTimeOffset(2026, 10, 22, 3, 0, 0, TimeSpan.Zero));

        (await Search(new DateTime(2026, 10, 22))).Should().BeEmpty();
    }

    [Fact]
    public async Task AmbiguousFareInAnyClass_ShouldSuppressWholeTripTypeLikeSeatMapAndCheckout()
    {
        AddSeat(_seed.PremiumClass, "P01");
        ReplaceFares(Fare("Commuter", 15000), Fare("Commuter", 20000, seatClass: _seed.PremiumClass),
            Fare("Commuter", 21000, seatClass: _seed.PremiumClass));
        var calls = _seed.Trip.TripStopCalls.OrderBy(c => c.VisitOrder).ToList();

        (await SearchAll()).Should().BeEmpty();
        var seatMap = () => Service().GetSeatMapAsync(_seed.Trip.Id,
            new GetTripSeatMapRequest(calls[0].Id, calls[2].Id), CancellationToken.None);
        await seatMap.Should().ThrowAsync<WaterbusSystem.Domain.Exceptions.ConcurrencyException>();
    }

    // ---- BR-TIME-01/02: trạng thái và mốc đóng bán ----

    [Theory]
    [InlineData(TripStatus.Scheduled, true)]
    [InlineData(TripStatus.Boarding, true)]
    [InlineData(TripStatus.EnRoute, false)]
    [InlineData(TripStatus.Suspended, false)]
    [InlineData(TripStatus.Cancelled, false)]
    public async Task OnlyScheduledAndBoardingTrips_ShouldBeSearchable(TripStatus status, bool expected)
    {
        _seed.Trip.Status = status;
        _context.SaveChanges();

        (await SearchAll()).Any().Should().Be(expected);
    }

    [Fact]
    public async Task SalesCloseAt_ShouldHideTripOnceReached()
    {
        _seed.Trip.SalesCloseAt = Now.AddMinutes(10);
        _context.SaveChanges();
        (await SearchAll()).Should().ContainSingle();

        _seed.Trip.SalesCloseAt = Now; // đúng mốc đóng bán: đã đóng (giống checkout/seat map)
        _context.SaveChanges();
        (await SearchAll()).Should().BeEmpty();
    }

    [Fact]
    public async Task Result_ShouldExposeSalesContract()
    {
        var trip = (await SearchAll()).Single();

        trip.CanBook.Should().BeTrue();
        trip.SalesState.Should().Be("open");
    }

    // ---- Tồn kho ghế thật ----

    [Fact]
    public async Task BoatWithoutActualSeats_ShouldNotBeSearchableEvenIfTotalSeatsIsSet()
    {
        _context.Seats.RemoveRange(_context.Seats.Where(s => s.BoatId == _seed.Boat.Id));
        _context.SaveChanges();

        _seed.Boat.TotalSeats.Should().Be(2);
        (await SearchAll()).Should().BeEmpty();
    }

    [Fact]
    public async Task InactiveSeat_ShouldNotCountAsAvailable()
    {
        _seed.Seat2.IsActive = false;
        _context.SaveChanges();

        (await SearchAll()).Single().AvailableSeatsCount.Should().Be(1);
    }

    [Fact]
    public async Task InactiveSeatClass_ShouldRemoveItsSeats()
    {
        _seed.StandardClass.IsActive = false;
        _context.SaveChanges();

        (await SearchAll()).Should().BeEmpty();
    }

    [Fact]
    public async Task FullySoldTrip_ShouldStayVisibleButNotBookable()
    {
        var booking = TestDbContextFactory.SeedBooking(_context, _seed, from: 1, to: 3);
        foreach (var seat in new[] { _seed.Seat1, _seed.Seat2 })
            _context.SeatReservations.Add(new SeatReservation
            {
                Booking = booking, BookingId = booking.Id, TripId = _seed.Trip.Id, SeatId = seat.Id,
                BoardingStopOrder = 1, DisembarkingStopOrder = 3
            });
        _context.SaveChanges();

        var trip = (await SearchAll()).Single();

        trip.AvailableSeatsCount.Should().Be(0);
        trip.CanBook.Should().BeFalse();
        trip.SalesState.Should().Be("soldOut");
        trip.BasePrice.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SearchAvailability_ShouldMatchSeatMap()
    {
        _seed.Seat2.IsActive = false;
        var booking = TestDbContextFactory.SeedBooking(_context, _seed, from: 1, to: 2);
        _context.SeatReservations.Add(new SeatReservation
        {
            Booking = booking, BookingId = booking.Id, TripId = _seed.Trip.Id, SeatId = _seed.Seat1.Id,
            BoardingStopOrder = 1, DisembarkingStopOrder = 2
        });
        _context.SaveChanges();
        var calls = _seed.Trip.TripStopCalls.OrderBy(c => c.VisitOrder).ToList();

        foreach (var (from, to) in new[] { (0, 2), (0, 1), (1, 2) })
        {
            var search = await Service().SearchAsync(new SearchTripsRequest(
                calls[from].RouteStop!.StationId, calls[to].RouteStop!.StationId, null, null), CancellationToken.None);
            var map = await Service().GetSeatMapAsync(_seed.Trip.Id,
                new GetTripSeatMapRequest(calls[from].Id, calls[to].Id), CancellationToken.None);

            var expected = search.SingleOrDefault()?.AvailableSeatsCount ?? 0;
            expected.Should().Be(map.AvailableSeatsCount, $"segment {from}->{to}");
        }
    }

    // ---- Hành trình: VisitOrder, chuyến vòng, thời lượng ----

    [Fact]
    public async Task CircularTrip_ShouldMatchRepeatedStationAndKeepVisitOrderInStops()
    {
        var stop = new RouteStop { RouteId = _seed.Route.Id, StationId = _seed.StationA.Id, SequenceNo = 4 };
        _context.RouteStops.Add(stop);
        _context.TripStopCalls.Add(new TripStopCall { TripId = _seed.Trip.Id, RouteStop = stop, VisitOrder = 4 });
        _context.SaveChanges();

        var found = await Service().SearchAsync(
            new SearchTripsRequest(_seed.StationA.Id, _seed.StationA.Id, null, null), CancellationToken.None);

        found.Single().Stops.Select(x => x.OrderIndex).Should().Equal(1, 2, 3, 4);
    }

    [Fact]
    public async Task Duration_ShouldBeNullForPartialJourneyWithoutTimetable_AndTripLengthForFullJourney()
    {
        var partial = await Service().SearchAsync(
            new SearchTripsRequest(_seed.StationB.Id, _seed.StationC.Id, null, null), CancellationToken.None);
        var full = await SearchAll();

        partial.Single().DurationMinutes.Should().BeNull();
        full.Single().DurationMinutes.Should().Be(45);
    }

    [Fact]
    public async Task Duration_ShouldUseStopTimetableForSegment()
    {
        var calls = _seed.Trip.TripStopCalls.OrderBy(c => c.VisitOrder).ToList();
        calls[1].EstimatedDepartureTime = _seed.Trip.DepartureTime.AddMinutes(10);
        calls[2].EstimatedArrivalTime = _seed.Trip.DepartureTime.AddMinutes(25);
        _context.SaveChanges();

        var found = await Service().SearchAsync(
            new SearchTripsRequest(_seed.StationB.Id, _seed.StationC.Id, null, null), CancellationToken.None);

        found.Single().DurationMinutes.Should().Be(15);
    }

    [Fact]
    public async Task Paging_ShouldSkipPreviousPages_AndBeOffByDefault()
    {
        var second = _context.Trips.Single();
        var clone = new Trip
        {
            RouteId = second.RouteId, BoatId = second.BoatId, DepartureTime = second.DepartureTime.AddHours(1),
            ArrivalTime = second.ArrivalTime.AddHours(1), TripType = second.TripType, Status = TripStatus.Scheduled
        };
        _context.Trips.Add(clone);
        foreach (var call in second.TripStopCalls)
            _context.TripStopCalls.Add(new TripStopCall { TripId = clone.Id, RouteStopId = call.RouteStopId, VisitOrder = call.VisitOrder });
        _context.SaveChanges();

        (await SearchAll()).Should().HaveCount(2);
        var page2 = await Service().SearchAsync(new SearchTripsRequest(null, null, null, null, Page: 2, PageSize: 1), CancellationToken.None);
        page2.Should().ContainSingle().Which.Id.Should().Be(clone.Id);
        (await Service().SearchAsync(new SearchTripsRequest(null, null, null, null, Page: 3, PageSize: 1), CancellationToken.None))
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData(int.MaxValue, 100)]
    [InlineData(int.MaxValue, 1)]
    [InlineData(1_000_000, 5)]
    public async Task Paging_VeryFarPage_ShouldBeEmpty_NotWrapAroundToTheFirstPage(int page, int pageSize)
    {
        // (Page - 1) * pageSize tràn int sẽ thành offset âm; Skip(âm) không bỏ phần tử nào nên trả lại trang đầu.
        (await SearchAll()).Should().HaveCount(1);

        (await Service().SearchAsync(new SearchTripsRequest(null, null, null, null, Page: page, PageSize: pageSize), CancellationToken.None))
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData(0, 10, false)]
    [InlineData(1, 0, false)]
    [InlineData(1, 101, false)]
    [InlineData(1, 100, true)]
    [InlineData(1, null, true)]
    public void Validator_ShouldBoundPaging(int page, int? pageSize, bool valid) =>
        new WaterbusSystem.Application.Validators.Trips.SearchTripsRequestValidator()
            .Validate(new SearchTripsRequest(null, null, null, null, page, pageSize)).IsValid.Should().Be(valid);

    // ---- H05: bến lên/bến xuống phải theo điểm dừng thực tế (TripStopCall), không theo
    // Route.DepartureStationId/ArrivalStationId + Station.OrderIndex toàn hệ thống ----

    [Fact]
    public async Task SearchByMidRouteStation_ShouldFindTripBoardingThere()
    {
        var found = await Service().SearchAsync(
            new SearchTripsRequest(_seed.StationB.Id, null, null, null), CancellationToken.None);

        found.Should().ContainSingle();
        found.Single().DepartureStationName.Should().Be(_seed.StationB.Name);
    }

    [Fact]
    public async Task SearchBoardingAfterDisembarking_ShouldNotMatch()
    {
        // Bến B (VisitOrder 2) sau bến A (VisitOrder 1) theo chiều chuyến -> không phải hành trình hợp lệ.
        var found = await Service().SearchAsync(
            new SearchTripsRequest(_seed.StationB.Id, _seed.StationA.Id, null, null), CancellationToken.None);

        found.Should().BeEmpty();
    }

    [Fact]
    public async Task SeatBookedOnlyForEarlierSegment_ShouldStayAvailableForLaterSegment()
    {
        var booking = TestDbContextFactory.SeedBooking(_context, _seed, from: 1, to: 2);
        _context.SeatReservations.Add(new SeatReservation
        {
            Booking = booking, BookingId = booking.Id, TripId = _seed.Trip.Id, SeatId = _seed.Seat1.Id,
            BoardingStopOrder = 1, DisembarkingStopOrder = 2
        });
        await _context.SaveChangesAsync();

        var laterSegment = await Service().SearchAsync(
            new SearchTripsRequest(_seed.StationB.Id, _seed.StationC.Id, null, null), CancellationToken.None);
        laterSegment.Single().AvailableSeatsCount.Should().Be(2);

        var wholeTrip = await Service().SearchAsync(
            new SearchTripsRequest(null, null, null, null), CancellationToken.None);
        wholeTrip.Single().AvailableSeatsCount.Should().Be(1);
    }
}
