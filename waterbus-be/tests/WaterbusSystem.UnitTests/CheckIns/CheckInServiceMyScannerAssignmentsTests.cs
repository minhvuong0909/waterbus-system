using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Requests.CheckIns;
using WaterbusSystem.Application.Features.CheckIns;
using WaterbusSystem.Application.Features.CheckIns.Services;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Infrastructure.Repositories;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;

namespace WaterbusSystem.UnitTests.CheckIns;

/// <summary>
/// Test CheckInService.GetMyScannerAssignmentsAsync - Staff chỉ thấy phân công của chính mình, đúng
/// phục vụ BR-OFF-01 (biết điểm dừng nào để preload manifest).
/// </summary>
public class CheckInServiceMyScannerAssignmentsTests
{
    private static CheckInService CreateService(
        WaterbusSystem.Infrastructure.Persistence.ApplicationDbContext context, Guid staffId, DateTimeOffset now, string? role = "Staff")
    {
        var user = new Mock<ICurrentUserService>();
        user.Setup(x => x.UserId).Returns(staffId.ToString());
        user.Setup(x => x.UserRole).Returns(role);

        var clock = new Mock<IDateTimeService>();
        clock.Setup(x => x.Now).Returns(now);

        return new CheckInService(new CheckInRepository(context), new WaterbusSystem.Infrastructure.Persistence.EfUnitOfWork(context),
            user.Object, clock.Object, new CheckInProcessor(new CheckInRepository(context)),
            new WaterbusSystem.Infrastructure.Persistence.SqlScannerAssignmentLock(context),
            Options.Create(new NoShowFinalizationOptions()), NullLogger<CheckInService>.Instance);
    }

    [Fact]
    public async Task GetMyScannerAssignmentsAsync_ShouldReturnOnlyAssignmentsOfCallingStaff()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var seed = TestDbContextFactory.SeedTrip(context);
        var stopCall = seed.Trip.TripStopCalls.Single(c => c.VisitOrder == 1);
        var device = new ScannerDevice { DeviceCode = "DEV-01", IsActive = true };
        context.ScannerDevices.Add(device);
        context.SaveChanges();

        var now = DateTimeOffset.UtcNow;
        var myStaffId = Guid.NewGuid();
        var otherStaffId = Guid.NewGuid();

        var assignService = CreateService(context, myStaffId, now);
        await assignService.AssignScannerAsync(
            new AssignScannerRequest(device.Id, myStaffId, stopCall.Id, true, now.AddHours(-1), now.AddHours(2)),
            CancellationToken.None);

        var device2 = new ScannerDevice { DeviceCode = "DEV-02", IsActive = true };
        context.ScannerDevices.Add(device2);
        context.SaveChanges();
        await assignService.AssignScannerAsync(
            new AssignScannerRequest(device2.Id, otherStaffId, stopCall.Id, false, now.AddHours(-1), now.AddHours(2)),
            CancellationToken.None);

        var service = CreateService(context, myStaffId, now);
        var result = await service.GetMyScannerAssignmentsAsync(CancellationToken.None);

        result.Should().ContainSingle();
        result[0].TripStopCallId.Should().Be(stopCall.Id);
        result[0].StationName.Should().Be(seed.StationA.Name);
        result[0].RouteName.Should().Be(seed.Route.Name);
        result[0].IsPrimaryOffline.Should().BeTrue();
    }

    [Fact]
    public async Task GetMyScannerAssignmentsAsync_WhenAssignmentAlreadyEnded_ShouldNotBeReturned()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var seed = TestDbContextFactory.SeedTrip(context);
        var stopCall = seed.Trip.TripStopCalls.Single(c => c.VisitOrder == 1);
        var device = new ScannerDevice { DeviceCode = "DEV-01", IsActive = true };
        context.ScannerDevices.Add(device);
        context.SaveChanges();

        var now = DateTimeOffset.UtcNow;
        var staffId = Guid.NewGuid();

        var assignService = CreateService(context, staffId, now);
        await assignService.AssignScannerAsync(
            new AssignScannerRequest(device.Id, staffId, stopCall.Id, true, now.AddHours(-3), now.AddHours(-1)),
            CancellationToken.None);

        var service = CreateService(context, staffId, now);
        var result = await service.GetMyScannerAssignmentsAsync(CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMyScannerAssignmentsAsync_WhenCallerIsNotStaff_ShouldThrowForbiddenAccessException()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var service = CreateService(context, Guid.NewGuid(), DateTimeOffset.UtcNow, role: "Admin");

        var act = () => service.GetMyScannerAssignmentsAsync(CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }
}
