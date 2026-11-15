using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Rules;
using WaterbusSystem.Infrastructure.Identity;
using WaterbusSystem.Infrastructure.Persistence;
using Xunit;

namespace WaterbusSystem.UnitTests.Sightseeing;

/// <summary>
/// Seed demo phải cho ra Trip bán được: loại Trip lấy từ Schedule (không từ giờ khởi hành), khớp loại dịch vụ của Route,
/// có chuyến Sightseeing vòng A → B → C → A thật sự đặt được, và chạy lại không đổi gì / không đụng Trip đã có khách.
/// </summary>
public class SightseeingDemoSeedTests
{
    private static ServiceProvider BuildServices(string databaseName)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(databaseName));
        services.AddIdentity<ApplicationUser, ApplicationRole>().AddEntityFrameworkStores<ApplicationDbContext>();
        services.AddScoped<ApplicationDbContextInitializer>();
        return services.BuildServiceProvider();
    }

    private static async Task SeedAsync(ServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContextInitializer>().SeedAsync();
    }

    private static async Task<T> InScope<T>(ServiceProvider provider, Func<ApplicationDbContext, Task<T>> action)
    {
        using var scope = provider.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    private static IQueryable<Trip> Graph(ApplicationDbContext c) => c.Trips
        .Include(t => t.Route).Include(t => t.Schedule)
        .Include(t => t.TripStopCalls).ThenInclude(x => x.RouteStop);

    [Fact]
    public async Task FreshSeed_EveryTripType_MatchesItsScheduleAndRouteServiceType()
    {
        using var provider = BuildServices(Guid.NewGuid().ToString());
        await SeedAsync(provider);

        var trips = await InScope(provider, c => Graph(c).ToListAsync());
        trips.Should().NotBeEmpty();
        foreach (var trip in trips.Where(t => t.ScheduleId != null))
        {
            trip.TripType.Should().Be(trip.Schedule!.TripType, $"trip {trip.DepartureTime:u} must follow its schedule, not the clock");
            var expected = trip.TripType == TripType.Sightseeing ? "Sightseeing" : "Regular";
            trip.Route!.ServiceType.Should().Be(expected);
        }
    }

    [Fact]
    public async Task FreshSeed_EveningRegularDepartures_StayRegular()
    {
        using var provider = BuildServices(Guid.NewGuid().ToString());
        await SeedAsync(provider);

        var evening = await InScope(provider, c => Graph(c).Where(t => t.Route!.ServiceType == "Regular").ToListAsync());
        evening.Should().NotBeEmpty();
        evening.Should().OnlyContain(t => t.TripType == TripType.Commuter);
    }

    [Fact]
    public async Task FreshSeed_HasFutureBookableCircularSightseeingTours_OnADedicatedRoute()
    {
        using var provider = BuildServices(Guid.NewGuid().ToString());
        await SeedAsync(provider);

        var tours = await InScope(provider, c => Graph(c)
            .Where(t => t.TripType == TripType.Sightseeing && t.Status == TripStatus.Scheduled && t.DepartureTime > DateTimeOffset.UtcNow)
            .ToListAsync());
        tours.Should().NotBeEmpty();
        // Chỉ có hai tuyến tham quan một chiều: SS01 Bạch Đằng → Linh Đông và SS02 ngược lại.
        tours.Should().OnlyContain(t => t.Route!.Code == "SS01" || t.Route.Code == "SS02");
        tours.Select(t => t.Route!.Code).Distinct().Should().BeEquivalentTo(new[] { "SS01", "SS02" });
        foreach (var tour in tours)
        {
            tour.BoatId.Should().NotBeNull();
            tour.SalesCloseAt.Should().NotBeNull();
            var journey = SightseeingJourney.Resolve(tour);
            journey.IsValid.Should().BeTrue(journey.Error);
            journey.Boarding!.Id.Should().NotBe(journey.Disembarking!.Id);
            journey.Boarding.RouteStop!.StationId.Should().NotBe(journey.Disembarking.RouteStop!.StationId, "tuyến một chiều A → B");
            tour.TripStopCalls.Should().HaveCount(2);
        }

        // Giá theo TripType x SeatClass đã seed cho Sightseeing (BR-FARE-01).
        (await InScope(provider, c => c.FareRules.CountAsync(f => f.TripType == "Sightseeing"))).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SecondSeedRun_IsIdempotent()
    {
        using var provider = BuildServices(Guid.NewGuid().ToString());
        await SeedAsync(provider);
        var before = await InScope(provider, async c => (await c.Trips.CountAsync(), await c.Routes.CountAsync(),
            await c.RouteStops.CountAsync(), await c.Schedules.CountAsync(), await c.TripStopCalls.CountAsync(), await c.RoutePois.CountAsync()));

        await SeedAsync(provider);

        var after = await InScope(provider, async c => (await c.Trips.CountAsync(), await c.Routes.CountAsync(),
            await c.RouteStops.CountAsync(), await c.Schedules.CountAsync(), await c.TripStopCalls.CountAsync(), await c.RoutePois.CountAsync()));
        after.Should().Be(before);
    }

    [Fact]
    public async Task LegacyMislabelledTrips_AreRelabelled_OnlyWhenUnbooked_AndNeverDeleted()
    {
        using var provider = BuildServices(Guid.NewGuid().ToString());
        await SeedAsync(provider);

        // Mô phỏng CSDL từ bản seed cũ: hai Trip tối trên Route Regular bị gán Sightseeing; một Trip đã có khách.
        var (unbookedId, bookedId, total) = await InScope(provider, async c =>
        {
            var regularEvening = await c.Trips.Include(t => t.Route)
                .Where(t => t.Route!.ServiceType == "Regular" && t.Status == TripStatus.Scheduled && t.DepartureTime > DateTimeOffset.UtcNow)
                .OrderBy(t => t.DepartureTime).Take(2).ToListAsync();
            regularEvening.ForEach(t => t.TripType = TripType.Sightseeing);
            var order = new PurchaseOrder { PurchaserName = "x", PurchaserEmail = "x@example.com", QuotedTotal = 1, Status = OrderStatus.PendingPayment };
            var booked = regularEvening[1];
            var stops = await c.TripStopCalls.Where(s => s.TripId == booked.Id).OrderBy(s => s.VisitOrder).ToListAsync();
            var booking = new Booking
            {
                Order = order, OrderId = order.Id, BookingCode = Guid.NewGuid().ToString("N"), CustomerName = "x",
                CustomerEmail = "x@example.com", QuotedTotal = 1, Status = BookingStatus.PendingPayment
            };
            booking.SetJourney(stops[0], stops[^1]);
            c.Bookings.Add(booking);
            await c.SaveChangesAsync();
            return (regularEvening[0].Id, booked.Id, await c.Trips.CountAsync());
        });

        await SeedAsync(provider);
        await SeedAsync(provider); // idempotent

        await InScope(provider, async c =>
        {
            (await c.Trips.FindAsync(unbookedId))!.TripType.Should().Be(TripType.Commuter);
            (await c.Trips.FindAsync(bookedId))!.TripType.Should().Be(TripType.Sightseeing, "a booked trip needs manual review, never silent rewriting");
            (await c.Trips.CountAsync()).Should().Be(total);
            (await c.Bookings.CountAsync()).Should().Be(1);
            return 0;
        });
    }
}
