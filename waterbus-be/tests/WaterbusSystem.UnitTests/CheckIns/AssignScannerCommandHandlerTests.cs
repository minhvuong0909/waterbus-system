using FluentAssertions;
using WaterbusSystem.Application.Features.CheckIns.Commands.AssignScanner;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;

namespace WaterbusSystem.UnitTests.CheckIns;

/// <summary>
/// Test AssignScannerCommandHandler đối chiếu BR-OFF-02: chỉ 1 Primary Offline Scanner hoạt động
/// cùng lúc cho cùng điểm dừng.
/// </summary>
public class AssignScannerCommandHandlerTests
{
    private static (TestDbContextFactory.TripSeedData Seed, TripStopCall StopCall, ScannerDevice Device) SeedDeviceAndStop(
        WaterbusSystem.Infrastructure.Persistence.ApplicationDbContext context)
    {
        var seed = TestDbContextFactory.SeedTrip(context);
        var stopCall = seed.Trip.TripStopCalls.Single(c => c.VisitOrder == 1);
        var device = new ScannerDevice { DeviceCode = "DEV-01", IsActive = true };
        context.ScannerDevices.Add(device);
        context.SaveChanges();
        return (seed, stopCall, device);
    }

    [Fact]
    public async Task Handle_WhenNoExistingAssignment_ShouldCreatePrimary()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var (_, stopCall, device) = SeedDeviceAndStop(context);
        var handler = new AssignScannerCommandHandler(context);

        var now = DateTimeOffset.UtcNow;
        var result = await handler.Handle(
            new AssignScannerCommand(device.Id, Guid.NewGuid(), stopCall.Id, true, now, now.AddHours(2)),
            CancellationToken.None);

        result.IsPrimaryOffline.Should().BeTrue();
        context.ScannerAssignments.Count().Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenAnotherPrimaryOverlapsSameStop_ShouldThrowConflictException()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var (_, stopCall, device) = SeedDeviceAndStop(context);
        var handler = new AssignScannerCommandHandler(context);

        var now = DateTimeOffset.UtcNow;
        await handler.Handle(
            new AssignScannerCommand(device.Id, Guid.NewGuid(), stopCall.Id, true, now, now.AddHours(2)),
            CancellationToken.None);

        var device2 = new ScannerDevice { DeviceCode = "DEV-02", IsActive = true };
        context.ScannerDevices.Add(device2);
        context.SaveChanges();

        var act = () => handler.Handle(
            new AssignScannerCommand(device2.Id, Guid.NewGuid(), stopCall.Id, true, now.AddMinutes(30), now.AddHours(3)),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Handle_WhenAnotherPrimaryDoesNotOverlapInTime_ShouldSucceed()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var (_, stopCall, device) = SeedDeviceAndStop(context);
        var handler = new AssignScannerCommandHandler(context);

        var now = DateTimeOffset.UtcNow;
        await handler.Handle(
            new AssignScannerCommand(device.Id, Guid.NewGuid(), stopCall.Id, true, now, now.AddHours(1)),
            CancellationToken.None);

        var device2 = new ScannerDevice { DeviceCode = "DEV-02", IsActive = true };
        context.ScannerDevices.Add(device2);
        context.SaveChanges();

        // Ca sau, không chồng thời gian với ca trước.
        var result = await handler.Handle(
            new AssignScannerCommand(device2.Id, Guid.NewGuid(), stopCall.Id, true, now.AddHours(1), now.AddHours(2)),
            CancellationToken.None);

        result.Should().NotBeNull();
        context.ScannerAssignments.Count().Should().Be(2);
    }

    [Fact]
    public async Task Handle_WhenMultipleNonPrimaryAssignmentsOverlap_ShouldAllSucceed()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var (_, stopCall, device) = SeedDeviceAndStop(context);
        var handler = new AssignScannerCommandHandler(context);

        var now = DateTimeOffset.UtcNow;
        await handler.Handle(
            new AssignScannerCommand(device.Id, Guid.NewGuid(), stopCall.Id, false, now, now.AddHours(2)),
            CancellationToken.None);

        var device2 = new ScannerDevice { DeviceCode = "DEV-02", IsActive = true };
        context.ScannerDevices.Add(device2);
        context.SaveChanges();

        var act = () => handler.Handle(
            new AssignScannerCommand(device2.Id, Guid.NewGuid(), stopCall.Id, false, now, now.AddHours(2)),
            CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_WhenDeviceIsInactive_ShouldThrowConflictException()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var (_, stopCall, device) = SeedDeviceAndStop(context);
        device.IsActive = false;
        context.SaveChanges();

        var handler = new AssignScannerCommandHandler(context);
        var act = () => handler.Handle(
            new AssignScannerCommand(device.Id, Guid.NewGuid(), stopCall.Id, true, DateTimeOffset.UtcNow, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }
}
