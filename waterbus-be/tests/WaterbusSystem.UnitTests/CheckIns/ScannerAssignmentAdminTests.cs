using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Infrastructure;
using WaterbusSystem.Infrastructure.Repositories;
using WaterbusSystem.UnitTests.TestHelpers;
using WaterbusSystem.WebApi.Controllers;
using Xunit;

namespace WaterbusSystem.UnitTests.CheckIns;

public class ScannerAssignmentAdminTests
{
    [Fact]
    public async Task StaffDirectory_OnlyReturnsActiveStaff()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var activeStaff = TestDbContextFactory.AddScannerStaff(context);
        TestDbContextFactory.AddScannerStaff(context, isActive: false);
        TestDbContextFactory.AddScannerStaff(context, roleName: "Admin");

        var page = await new CheckInRepository(context).ListScannerStaffAsync(1, 20);

        page.TotalCount.Should().Be(1);
        page.Items.Should().ContainSingle().Which.Id.Should().Be(activeStaff);
    }

    [Fact]
    public async Task StopAssignments_OnlyReturnsCurrentAndUpcomingForRequestedStop()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var seed = TestDbContextFactory.SeedTrip(context);
        var stops = seed.Trip.TripStopCalls.OrderBy(stop => stop.VisitOrder).ToArray();
        var staff = TestDbContextFactory.AddScannerStaff(context);
        var device = new ScannerDevice { DeviceCode = "DEMO", IsActive = true };
        context.ScannerDevices.Add(device);
        var now = DateTimeOffset.UtcNow;
        ScannerAssignment Assignment(Guid stopId, DateTimeOffset from, DateTimeOffset? until) => new()
        {
            DeviceId = device.Id, StaffAccountId = staff, TripStopCallId = stopId,
            ActiveFrom = from, ActiveUntil = until
        };

        var current = Assignment(stops[0].Id, now.AddMinutes(-5), null);
        var upcoming = Assignment(stops[0].Id, now.AddHours(1), now.AddHours(2));
        context.ScannerAssignments.AddRange(current, upcoming, Assignment(stops[1].Id, now, null),
            Assignment(stops[0].Id, now.AddHours(-2), now.AddHours(-1)),
            Assignment(stops[0].Id, now.AddHours(3), now.AddHours(3)));
        await context.SaveChangesAsync();

        var page = await new CheckInRepository(context).ListScannerAssignmentsAsync(stops[0].Id, now, 1, 20);
        page.Items.Select(item => item.AssignmentId).Should().Equal(current.Id, upcoming.Id);
        page.Items.Should().AllSatisfy(item =>
        {
            item.DeviceCode.Should().Be(device.DeviceCode);
            item.StaffName.Should().NotBeNullOrWhiteSpace();
        });
    }

    [Theory]
    [InlineData(nameof(CheckInsController.ListScannerStaff))]
    [InlineData(nameof(CheckInsController.ListScannerAssignments))]
    public async Task AdminReads_RequireAdminRole(string action)
    {
        var policy = typeof(CheckInsController).GetMethod(action)!.GetCustomAttribute<AuthorizeAttribute>()!.Policy!;
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=Unused",
            ["JwtSettings:Secret"] = "test-signing-key-for-authorization-only"
        }).Build();
        var services = new ServiceCollection().AddLogging().AddInfrastructureServices(config);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var auth = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
        var staff = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Staff")], "Test"));
        var admin = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Admin")], "Test"));

        (await auth.AuthorizeAsync(new ClaimsPrincipal(), policy)).Succeeded.Should().BeFalse();
        (await auth.AuthorizeAsync(staff, policy)).Succeeded.Should().BeFalse();
        (await auth.AuthorizeAsync(admin, policy)).Succeeded.Should().BeTrue();
    }
}
