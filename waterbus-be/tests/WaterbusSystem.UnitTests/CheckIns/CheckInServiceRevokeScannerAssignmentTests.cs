using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Requests.CheckIns;
using WaterbusSystem.Application.Features.CheckIns;
using WaterbusSystem.Application.Features.CheckIns.Services;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure.Repositories;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;

namespace WaterbusSystem.UnitTests.CheckIns;

/// <summary>
/// Test CheckInService.RevokeScannerAssignmentAsync - cho phép Admin giải phóng slot Primary Offline
/// trước khi gán lại (BR-OFF-02).
/// </summary>
public class CheckInServiceRevokeScannerAssignmentTests
{
    private static CheckInService CreateService(
        WaterbusSystem.Infrastructure.Persistence.ApplicationDbContext context, DateTimeOffset now)
    {
        var clock = new Mock<IDateTimeService>();
        clock.Setup(x => x.Now).Returns(now);
        return new CheckInService(new CheckInRepository(context), new WaterbusSystem.Infrastructure.Persistence.EfUnitOfWork(context),
            Mock.Of<ICurrentUserService>(), clock.Object, new CheckInProcessor(new CheckInRepository(context)),
            new WaterbusSystem.Infrastructure.Persistence.SqlScannerAssignmentLock(context),
            Options.Create(new NoShowFinalizationOptions()), NullLogger<CheckInService>.Instance);
    }

    [Fact]
    public async Task RevokeScannerAssignmentAsync_WhenAssignmentIsActive_ShouldEndItNow()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var seed = TestDbContextFactory.SeedTrip(context);
        var stopCall = seed.Trip.TripStopCalls.Single(c => c.VisitOrder == 1);
        var device = new ScannerDevice { DeviceCode = "DEV-01", IsActive = true };
        context.ScannerDevices.Add(device);
        context.SaveChanges();

        var now = DateTimeOffset.UtcNow;
        var service = CreateService(context, now);
        var created = await service.AssignScannerAsync(
            new AssignScannerRequest(device.Id, TestDbContextFactory.AddScannerStaff(context), stopCall.Id, true, now.AddHours(-1), now.AddHours(2)),
            CancellationToken.None);

        var result = await service.RevokeScannerAssignmentAsync(created.AssignmentId, CancellationToken.None);

        result.ActiveUntil.Should().Be(now);
    }

    [Fact]
    public async Task RevokeScannerAssignmentAsync_AfterRevoke_ShouldAllowAssigningNewPrimaryImmediately()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var seed = TestDbContextFactory.SeedTrip(context);
        var stopCall = seed.Trip.TripStopCalls.Single(c => c.VisitOrder == 1);
        var device = new ScannerDevice { DeviceCode = "DEV-01", IsActive = true };
        context.ScannerDevices.Add(device);
        context.SaveChanges();

        var now = DateTimeOffset.UtcNow;
        var service = CreateService(context, now);
        var created = await service.AssignScannerAsync(
            new AssignScannerRequest(device.Id, TestDbContextFactory.AddScannerStaff(context), stopCall.Id, true, now.AddHours(-1), now.AddHours(2)),
            CancellationToken.None);

        await service.RevokeScannerAssignmentAsync(created.AssignmentId, CancellationToken.None);

        var device2 = new ScannerDevice { DeviceCode = "DEV-02", IsActive = true };
        context.ScannerDevices.Add(device2);
        context.SaveChanges();

        var act = () => service.AssignScannerAsync(
            new AssignScannerRequest(device2.Id, TestDbContextFactory.AddScannerStaff(context), stopCall.Id, true, now, now.AddHours(2)),
            CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RevokedFutureAssignment_DoesNotBlockAReplacement_AndRepeatedRevokePreservesWindow()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var seed = TestDbContextFactory.SeedTrip(context);
        var stopCall = seed.Trip.TripStopCalls.Single(c => c.VisitOrder == 1);
        var device = new ScannerDevice { DeviceCode = "DEV-01", IsActive = true };
        context.ScannerDevices.Add(device);
        var staffId = TestDbContextFactory.AddScannerStaff(context);
        var now = DateTimeOffset.UtcNow;
        var service = CreateService(context, now);
        var request = new AssignScannerRequest(device.Id, staffId, stopCall.Id, true, now.AddHours(1), now.AddHours(2));
        var future = await service.AssignScannerAsync(request);

        await service.RevokeScannerAssignmentAsync(future.AssignmentId);
        var revoked = await service.RevokeScannerAssignmentAsync(future.AssignmentId);
        revoked.ActiveUntil.Should().Be(request.ActiveFrom);
        (await service.ListScannerAssignmentsAsync(stopCall.Id, 1, 20)).Items.Should().BeEmpty();

        var replacement = await service.AssignScannerAsync(request with { ActiveFrom = now });
        (await service.ListScannerAssignmentsAsync(stopCall.Id, 1, 20)).Items
            .Should().ContainSingle().Which.AssignmentId.Should().Be(replacement.AssignmentId);
    }

    [Fact]
    public async Task RevokeScannerAssignmentAsync_WhenAssignmentDoesNotExist_ShouldThrowNotFoundException()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var service = CreateService(context, DateTimeOffset.UtcNow);

        var act = () => service.RevokeScannerAssignmentAsync(Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
