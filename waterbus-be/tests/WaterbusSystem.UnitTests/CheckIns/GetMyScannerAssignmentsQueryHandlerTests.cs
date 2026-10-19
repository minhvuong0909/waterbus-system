using FluentAssertions;
using Moq;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.CheckIns.Commands.AssignScanner;
using WaterbusSystem.Application.Features.CheckIns.Queries.GetMyScannerAssignments;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;

namespace WaterbusSystem.UnitTests.CheckIns;

/// <summary>
/// Test GetMyScannerAssignmentsQueryHandler - Staff chỉ thấy phân công của chính mình, đúng
/// phục vụ BR-OFF-01 (biết điểm dừng nào để preload manifest).
/// </summary>
public class GetMyScannerAssignmentsQueryHandlerTests
{
    private static GetMyScannerAssignmentsQueryHandler CreateHandler(
        WaterbusSystem.Infrastructure.Persistence.ApplicationDbContext context, Guid staffId, DateTimeOffset now, string? role = "Staff")
    {
        var user = new Mock<ICurrentUserService>();
        user.Setup(x => x.UserId).Returns(staffId.ToString());
        user.Setup(x => x.UserRole).Returns(role);

        var clock = new Mock<IDateTimeService>();
        clock.Setup(x => x.Now).Returns(now);

        return new GetMyScannerAssignmentsQueryHandler(context, user.Object, clock.Object);
    }

    [Fact]
    public async Task Handle_ShouldReturnOnlyAssignmentsOfCallingStaff()
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

        var assignHandler = new AssignScannerCommandHandler(context);
        await assignHandler.Handle(
            new AssignScannerCommand(device.Id, myStaffId, stopCall.Id, true, now.AddHours(-1), now.AddHours(2)),
            CancellationToken.None);

        var device2 = new ScannerDevice { DeviceCode = "DEV-02", IsActive = true };
        context.ScannerDevices.Add(device2);
        context.SaveChanges();
        await assignHandler.Handle(
            new AssignScannerCommand(device2.Id, otherStaffId, stopCall.Id, false, now.AddHours(-1), now.AddHours(2)),
            CancellationToken.None);

        var handler = CreateHandler(context, myStaffId, now);
        var result = await handler.Handle(new GetMyScannerAssignmentsQuery(), CancellationToken.None);

        result.Should().ContainSingle();
        result[0].TripStopCallId.Should().Be(stopCall.Id);
        result[0].StationName.Should().Be(seed.StationA.Name);
        result[0].RouteName.Should().Be(seed.Route.Name);
        result[0].IsPrimaryOffline.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenAssignmentAlreadyEnded_ShouldNotBeReturned()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var seed = TestDbContextFactory.SeedTrip(context);
        var stopCall = seed.Trip.TripStopCalls.Single(c => c.VisitOrder == 1);
        var device = new ScannerDevice { DeviceCode = "DEV-01", IsActive = true };
        context.ScannerDevices.Add(device);
        context.SaveChanges();

        var now = DateTimeOffset.UtcNow;
        var staffId = Guid.NewGuid();

        var assignHandler = new AssignScannerCommandHandler(context);
        await assignHandler.Handle(
            new AssignScannerCommand(device.Id, staffId, stopCall.Id, true, now.AddHours(-3), now.AddHours(-1)),
            CancellationToken.None);

        var handler = CreateHandler(context, staffId, now);
        var result = await handler.Handle(new GetMyScannerAssignmentsQuery(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenCallerIsNotStaff_ShouldThrowForbiddenAccessException()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var handler = CreateHandler(context, Guid.NewGuid(), DateTimeOffset.UtcNow, role: "Admin");

        var act = () => handler.Handle(new GetMyScannerAssignmentsQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }
}
