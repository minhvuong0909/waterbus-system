using FluentAssertions;
using Moq;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.CheckIns.Commands.SubmitCheckIn;
using WaterbusSystem.Application.Features.CheckIns.Services;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;
using static WaterbusSystem.UnitTests.TestHelpers.CheckInScenarioFactory;

namespace WaterbusSystem.UnitTests.CheckIns;

/// <summary>
/// Test SubmitCheckInCommandHandler đối chiếu BR-QR-03, BR-BOARD-03/05, BR-OFF-03.
/// </summary>
public class SubmitCheckInCommandHandlerTests
{
    private readonly Guid _staffId = Guid.NewGuid();

    private SubmitCheckInCommandHandler CreateHandler(ApplicationDbContext context, DateTimeOffset now, string? role = "Staff")
    {
        var user = new Mock<ICurrentUserService>();
        user.Setup(x => x.UserId).Returns(_staffId.ToString());
        user.Setup(x => x.UserRole).Returns(role);

        var clock = new Mock<IDateTimeService>();
        clock.Setup(x => x.Now).Returns(now);

        return new SubmitCheckInCommandHandler(context, user.Object, clock.Object, new CheckInProcessor(context));
    }

    [Fact]
    public async Task Handle_WhenValidRequest_ShouldCheckInSuccessfully()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var (_, _, ticket, boardingCall, now) = SeedCheckInScenario(context, _staffId);

        var handler = CreateHandler(context, now);
        var command = new SubmitCheckInCommand(ticket.Id, boardingCall.Id, "evt-1", now, null);

        var result = await handler.Handle(command, CancellationToken.None);

        result.Outcome.Should().Be("Success");
        result.CheckedInAt.Should().Be(now);

        var updated = context.Tickets.Single(t => t.Id == ticket.Id);
        updated.BoardingStatus.Should().Be(BoardingStatus.CheckedIn);
        updated.CheckedInByStaffId.Should().Be(_staffId);
    }

    [Fact]
    public async Task Handle_WhenScannedAtWrongStop_ShouldReturnInvalidStopAndNotMutateTicket()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var (seed, _, ticket, _, now) = SeedCheckInScenario(context, _staffId);

        var wrongStop = seed.Trip.TripStopCalls.Single(c => c.VisitOrder == 2); // Booking lên tại bến 1, không phải bến 2

        // Staff được phân công hợp lệ tại bến 2 (để tách riêng case "sai bến" khỏi case "không có quyền tác nghiệp").
        var device2 = new ScannerDevice { DeviceCode = "DEV-02", IsActive = true };
        context.ScannerDevices.Add(device2);
        context.ScannerAssignments.Add(new ScannerAssignment
        {
            DeviceId = device2.Id, Device = device2, StaffAccountId = _staffId,
            TripStopCallId = wrongStop.Id, TripStopCall = wrongStop, IsPrimaryOffline = true,
            ActiveFrom = now.AddHours(-1), ActiveUntil = now.AddHours(1)
        });
        context.SaveChanges();

        var handler = CreateHandler(context, now);
        var result = await handler.Handle(
            new SubmitCheckInCommand(ticket.Id, wrongStop.Id, "evt-2", now, null), CancellationToken.None);

        result.Outcome.Should().Be("InvalidStop");
        context.Tickets.Single(t => t.Id == ticket.Id).BoardingStatus.Should().Be(BoardingStatus.NotCheckedIn);
    }

    [Fact]
    public async Task Handle_WhenAlreadyCheckedIn_ShouldReturnAlreadyCheckedInWithoutThrowing()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var (_, _, ticket, boardingCall, now) = SeedCheckInScenario(context, _staffId);

        var handler = CreateHandler(context, now);
        await handler.Handle(new SubmitCheckInCommand(ticket.Id, boardingCall.Id, "evt-first", now, null), CancellationToken.None);

        // Quét lại (BR-QR-03 cho phép) với ClientEventId khác (lần quét mới, không phải retry mạng).
        var result = await handler.Handle(
            new SubmitCheckInCommand(ticket.Id, boardingCall.Id, "evt-second", now.AddMinutes(1), null), CancellationToken.None);

        result.Outcome.Should().Be("AlreadyCheckedIn");
    }

    [Fact]
    public async Task Handle_WhenStaffHasNoAssignmentForStop_ShouldReturnNotAssigned()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var (_, _, ticket, boardingCall, now) = SeedCheckInScenario(context, _staffId, withAssignment: false);

        var handler = CreateHandler(context, now);
        var result = await handler.Handle(
            new SubmitCheckInCommand(ticket.Id, boardingCall.Id, "evt-3", now, null), CancellationToken.None);

        result.Outcome.Should().Be("NotAssigned");
        context.Tickets.Single(t => t.Id == ticket.Id).BoardingStatus.Should().Be(BoardingStatus.NotCheckedIn);
    }

    [Fact]
    public async Task Handle_WhenSameClientEventIdSentTwice_ShouldBeIdempotentAndNotDuplicateEvent()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var (_, _, ticket, boardingCall, now) = SeedCheckInScenario(context, _staffId);

        var handler = CreateHandler(context, now);
        var command = new SubmitCheckInCommand(ticket.Id, boardingCall.Id, "evt-retry", now, null);

        var first = await handler.Handle(command, CancellationToken.None);
        var second = await handler.Handle(command, CancellationToken.None);

        second.Outcome.Should().Be(first.Outcome);
        context.CheckInEvents.Count(e => e.ClientEventId == "evt-retry").Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenCallerIsNotStaff_ShouldThrowForbiddenAccessException()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var (_, _, ticket, boardingCall, now) = SeedCheckInScenario(context, _staffId);

        var handler = CreateHandler(context, now, role: "Admin");
        var act = () => handler.Handle(
            new SubmitCheckInCommand(ticket.Id, boardingCall.Id, "evt-4", now, null), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }
}
