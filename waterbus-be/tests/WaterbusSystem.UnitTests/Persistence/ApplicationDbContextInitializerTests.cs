using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WaterbusSystem.Infrastructure.Identity;
using WaterbusSystem.Infrastructure.Persistence;
using Xunit;

namespace WaterbusSystem.UnitTests.Persistence;

/// <summary>
/// Seed demo phải chạy lại được trên CSDL đã có dữ liệu (mỗi lần API khởi động đều gọi SeedAsync).
/// </summary>
public class ApplicationDbContextInitializerTests
{
    private static ServiceProvider BuildServices(string databaseName)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(databaseName));
        services.AddIdentity<ApplicationUser, ApplicationRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>();
        services.AddScoped<ApplicationDbContextInitializer>();
        return services.BuildServiceProvider();
    }

    private static async Task SeedInNewScopeAsync(ServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContextInitializer>().SeedAsync();
    }

    [Fact]
    public async Task SeedAsync_RunTwice_ShouldNotThrowOrDuplicateTripStopCalls()
    {
        using var provider = BuildServices(Guid.NewGuid().ToString());

        await SeedInNewScopeAsync(provider);
        await SeedInNewScopeAsync(provider);

        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var calls = await context.TripStopCalls.ToListAsync();
        calls.Should().NotBeEmpty();
        calls.GroupBy(c => new { c.TripId, c.VisitOrder }).Should().OnlyContain(g => g.Count() == 1);
    }

    [Fact]
    public async Task SeedAsync_ExistingTripWithoutStopCalls_ShouldCreateThem()
    {
        using var provider = BuildServices(Guid.NewGuid().ToString());
        await SeedInNewScopeAsync(provider);

        // Mô phỏng CSDL đã có Trip từ trước nhưng chưa có TripStopCall (trường hợp gây lỗi trước đây)
        using (var scope = provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.TripStopCalls.RemoveRange(context.TripStopCalls);
            await context.SaveChangesAsync();
        }

        await SeedInNewScopeAsync(provider);

        using var verifyScope = provider.CreateScope();
        var verify = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await verify.TripStopCalls.CountAsync()).Should().BeGreaterThan(0);
    }

    private static async Task<T> InNewScopeAsync<T>(ServiceProvider provider, Func<ApplicationDbContext, Task<T>> action)
    {
        using var scope = provider.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    [Fact]
    public async Task SeedAsync_FreshDatabase_ShouldUseRealStationCoordinates()
    {
        using var provider = BuildServices(Guid.NewGuid().ToString());

        await SeedInNewScopeAsync(provider);

        var stations = await InNewScopeAsync(provider, c => c.Stations.OrderBy(s => s.Code).ToListAsync());
        stations.Select(s => (s.Latitude, s.Longitude)).Should().Equal(
            ApplicationDbContextInitializer.DemoStationCoordinates.OrderBy(c => c.Code).Select(c => (c.NewLat, c.NewLng)));
    }

    [Fact]
    public async Task SeedAsync_DatabaseWithOldSeedCoordinates_ShouldCorrectThem_AndRespectAdminEdits()
    {
        using var provider = BuildServices(Guid.NewGuid().ToString());
        await SeedInNewScopeAsync(provider);

        // Simulate a database created by the old seed, where the admin has already moved ST03 on purpose.
        await InNewScopeAsync(provider, async c =>
        {
            foreach (var (code, oldLat, oldLng, _, _) in ApplicationDbContextInitializer.DemoStationCoordinates)
            {
                var station = await c.Stations.SingleAsync(s => s.Code == code);
                (station.Latitude, station.Longitude) = code == "ST03" ? (10.9, 106.8) : (oldLat, oldLng);
            }
            await c.SaveChangesAsync();
            return 0;
        });

        await SeedInNewScopeAsync(provider);
        await SeedInNewScopeAsync(provider); // idempotent

        var byCode = (await InNewScopeAsync(provider, c => c.Stations.ToListAsync())).ToDictionary(s => s.Code);
        foreach (var (code, _, _, newLat, newLng) in ApplicationDbContextInitializer.DemoStationCoordinates.Where(c => c.Code != "ST03"))
        {
            byCode[code].Latitude.Should().Be(newLat);
            byCode[code].Longitude.Should().Be(newLng);
        }
        byCode["ST03"].Latitude.Should().Be(10.9);
        byCode["ST03"].Longitude.Should().Be(106.8);
    }

    [Fact]
    public async Task SeedAsync_FreshDatabase_StopTimesShouldSpanTheWholeTripWindow()
    {
        using var provider = BuildServices(Guid.NewGuid().ToString());
        await SeedInNewScopeAsync(provider);

        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var trip = await context.Trips.Include(t => t.TripStopCalls).FirstAsync(t => t.TripStopCalls.Any());
        var calls = trip.TripStopCalls.OrderBy(c => c.VisitOrder).ToList();

        calls.First().EstimatedDepartureTime.Should().Be(trip.DepartureTime);
        calls.Last().EstimatedArrivalTime.Should().Be(trip.ArrivalTime);
    }

    [Fact]
    public async Task SeedAsync_StopTimesFromTheOldSeed_ShouldBeAlignedOnceAndStayStable()
    {
        using var provider = BuildServices(Guid.NewGuid().ToString());
        await SeedInNewScopeAsync(provider);

        // Dữ liệu cũ: bến đầu rời +2 phút, bến cuối đến +44 phút.
        int?[] oldArrival = [null, 11, 22, 33, 44];
        int?[] oldDeparture = [2, 13, 24, 35, null];
        Guid scheduleId;
        using (var scope = provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            scheduleId = (await context.Schedules.FirstAsync(s => s.Route!.Code == "RT01" && s.DepartureTime == new TimeSpan(8, 0, 0))).Id;
            // Chỉ lịch demo 08:00 của RT01 (seed còn có các lịch khác và tuyến RT02).
            var stops = await context.ScheduleStops.Where(s => s.ScheduleId == scheduleId).OrderBy(s => s.VisitOrder).ToListAsync();
            for (var i = 0; i < stops.Count; i++)
            {
                stops[i].ArrivalOffsetMin = oldArrival[i];
                stops[i].DepartureOffsetMin = oldDeparture[i];
            }
            foreach (var call in await context.TripStopCalls.Include(c => c.Trip)
                         .Where(c => c.Trip!.ScheduleId == scheduleId).ToListAsync())
            {
                var i = call.VisitOrder - 1;
                call.EstimatedArrivalTime = oldArrival[i] is { } a ? call.Trip!.DepartureTime.AddMinutes(a) : null;
                call.EstimatedDepartureTime = oldDeparture[i] is { } d ? call.Trip!.DepartureTime.AddMinutes(d) : null;
            }
            await context.SaveChangesAsync();
        }

        await SeedInNewScopeAsync(provider);
        await SeedInNewScopeAsync(provider);

        using var check = provider.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var trip = await db.Trips.Include(t => t.TripStopCalls).FirstAsync(t => t.ScheduleId == scheduleId && t.TripStopCalls.Any());
        var calls = trip.TripStopCalls.OrderBy(c => c.VisitOrder).ToList();
        calls.First().EstimatedDepartureTime.Should().Be(trip.DepartureTime);
        calls.Last().EstimatedArrivalTime.Should().Be(trip.ArrivalTime);
        (await db.ScheduleStops.Where(s => s.ScheduleId == scheduleId).OrderBy(s => s.VisitOrder).Select(s => s.ArrivalOffsetMin).ToListAsync())
            .Should().Equal(null, 11, 23, 34, 45);
    }

    [Fact]
    public async Task SeedAsync_ScheduleOffsetsEditedByAdmin_ShouldNotBeOverwritten()
    {
        using var provider = BuildServices(Guid.NewGuid().ToString());
        await SeedInNewScopeAsync(provider);

        using (var scope = provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var last = await context.ScheduleStops.OrderByDescending(s => s.VisitOrder).FirstAsync();
            last.ArrivalOffsetMin = 50; // Admin kéo dài hành trình
            await context.SaveChangesAsync();
        }

        await SeedInNewScopeAsync(provider);

        using var check = provider.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.ScheduleStops.OrderByDescending(s => s.VisitOrder).Select(s => s.ArrivalOffsetMin).FirstAsync())
            .Should().Be(50);
    }

    [Theory]
    [InlineData(0, null, 0)]
    [InlineData(1, 11, 13)]
    [InlineData(2, 23, 25)]
    [InlineData(3, 34, 36)]
    [InlineData(4, 45, null)]
    public void DemoStopOffsets_FiveStops_ShouldRunFromZeroToTheRouteDuration(int index, int? arrival, int? departure)
    {
        ApplicationDbContextInitializer.DemoStopOffsets(index, 5).Should().Be((arrival, departure));
    }
}
