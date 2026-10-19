using FluentAssertions;
using Moq;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.CheckIns.Commands.AssignScanner;
using WaterbusSystem.Application.Features.CheckIns.Commands.RevokeScannerAssignment;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;

namespace WaterbusSystem.UnitTests.CheckIns;

/// <summary>
/// Test RevokeScannerAssignmentCommandHandler - cho phép Admin giải phóng slot Primary Offline
/// trước khi gán lại (BR-OFF-02).
/// </summary>
public class RevokeScannerAssignmentCommandHandlerTests
{
    private static RevokeScannerAssignmentCommandHandler CreateHandler(
        WaterbusSystem.Infrastructure.Persistence.ApplicationDbContext context, DateTimeOffset now)
    {
        var clock = new Mock<IDateTimeService>();
        clock.Setup(x => x.Now).Returns(now);
        return new RevokeScannerAssignmentCommandHandler(context, clock.Object);
    }

    [Fact]
    public async Task Handle_WhenAssignmentIsActive_ShouldEndItNow()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var seed = TestDbContextFactory.SeedTrip(context);
        var stopCall = seed.Trip.TripStopCalls.Single(c => c.VisitOrder == 1);
        var device = new ScannerDevice { DeviceCode = "DEV-01", IsActive = true };
        context.ScannerDevices.Add(device);
        context.SaveChanges();

        var now = DateTimeOffset.UtcNow;
        var assignHandler = new AssignScannerCommandHandler(context);
        var created = await assignHandler.Handle(
            new AssignScannerCommand(device.Id, Guid.NewGuid(), stopCall.Id, true, now.AddHours(-1), now.AddHours(2)),
            CancellationToken.None);

        var revokeHandler = CreateHandler(context, now);
        var result = await revokeHandler.Handle(new RevokeScannerAssignmentCommand(created.AssignmentId), CancellationToken.None);

        result.ActiveUntil.Should().Be(now);
    }

    [Fact]
    public async Task Handle_AfterRevoke_ShouldAllowAssigningNewPrimaryImmediately()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var seed = TestDbContextFactory.SeedTrip(context);
        var stopCall = seed.Trip.TripStopCalls.Single(c => c.VisitOrder == 1);
        var device = new ScannerDevice { DeviceCode = "DEV-01", IsActive = true };
        context.ScannerDevices.Add(device);
        context.SaveChanges();

        var now = DateTimeOffset.UtcNow;
        var assignHandler = new AssignScannerCommandHandler(context);
        var created = await assignHandler.Handle(
            new AssignScannerCommand(device.Id, Guid.NewGuid(), stopCall.Id, true, now.AddHours(-1), now.AddHours(2)),
            CancellationToken.None);

        var revokeHandler = CreateHandler(context, now);
        await revokeHandler.Handle(new RevokeScannerAssignmentCommand(created.AssignmentId), CancellationToken.None);

        var device2 = new ScannerDevice { DeviceCode = "DEV-02", IsActive = true };
        context.ScannerDevices.Add(device2);
        context.SaveChanges();

        var act = () => assignHandler.Handle(
            new AssignScannerCommand(device2.Id, Guid.NewGuid(), stopCall.Id, true, now, now.AddHours(2)),
            CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_WhenAssignmentDoesNotExist_ShouldThrowNotFoundException()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var handler = CreateHandler(context, DateTimeOffset.UtcNow);

        var act = () => handler.Handle(new RevokeScannerAssignmentCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
