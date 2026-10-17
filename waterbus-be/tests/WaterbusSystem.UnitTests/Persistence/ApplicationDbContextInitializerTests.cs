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
}
