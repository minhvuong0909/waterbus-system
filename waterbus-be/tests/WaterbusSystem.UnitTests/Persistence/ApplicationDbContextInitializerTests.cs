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
}
