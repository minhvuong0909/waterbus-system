using FluentAssertions;
using Moq;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Trips.Queries.SearchTrips;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;

namespace WaterbusSystem.UnitTests.Trips;

public class SearchTripsQueryHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 5, 0, 0, TimeSpan.Zero);

    private readonly ApplicationDbContext _context = TestDbContextFactory.CreateDbContext();
    private readonly TestDbContextFactory.TripSeedData _seed;
    private readonly Mock<IDateTimeService> _clock = new();

    public SearchTripsQueryHandlerTests()
    {
        _clock.Setup(c => c.Now).Returns(() => Now);
        _seed = TestDbContextFactory.SeedTrip(_context);
        _seed.Trip.DepartureTime = Now.AddDays(5); // tránh trùng các ngày dùng trong test
        _seed.Trip.ArrivalTime = Now.AddDays(5).AddMinutes(45);
        _context.SaveChanges();
    }

    private SearchTripsQueryHandler Handler() => new(_context, _clock.Object);

    private Trip SetDeparture(DateTimeOffset departure)
    {
        _seed.Trip.DepartureTime = departure;
        _seed.Trip.ArrivalTime = departure.AddMinutes(45);
        _seed.Trip.TripType = TripType.Commuter;
        _context.SaveChanges();
        return _seed.Trip;
    }

    private Task<List<TripDto>> Search(DateTime date) =>
        Handler().Handle(new SearchTripsQuery(null, null, date, null), CancellationToken.None);

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

    private FareRule Fare(string tripType, decimal price, bool active = true, DateTimeOffset? from = null, DateTimeOffset? to = null) => new()
    {
        TripType = tripType, SeatClassId = _context.SeatClasses.First().Id, Price = price, IsActive = active,
        EffectiveFrom = from ?? Now.AddDays(-30), EffectiveTo = to
    };

    [Fact]
    public async Task BasePrice_ShouldBeLowestActiveFareOfTheTripType()
    {
        ReplaceFares(Fare("Commuter", 20000), Fare("Commuter", 15000), Fare("Sightseeing", 100000));
        SetDeparture(new DateTimeOffset(2026, 10, 22, 3, 0, 0, TimeSpan.Zero));

        var trip = (await Search(new DateTime(2026, 10, 22))).Single();

        trip.BasePrice.Should().Be(15000);
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
    public async Task BasePrice_ShouldBeZeroWhenNoFareApplies()
    {
        ReplaceFares(Fare("Sightseeing", 100000));
        SetDeparture(new DateTimeOffset(2026, 10, 22, 3, 0, 0, TimeSpan.Zero));

        (await Search(new DateTime(2026, 10, 22))).Single().BasePrice.Should().Be(0);
    }
}
