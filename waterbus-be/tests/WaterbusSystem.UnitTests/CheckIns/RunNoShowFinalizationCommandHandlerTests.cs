using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.CheckIns;
using WaterbusSystem.Application.Features.CheckIns.Commands.RunNoShowFinalization;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;
using static WaterbusSystem.UnitTests.TestHelpers.CheckInScenarioFactory;

namespace WaterbusSystem.UnitTests.CheckIns;

/// <summary>
/// Test RunNoShowFinalizationCommandHandler đối chiếu BR-BOARD-05 (chỉ kết luận NoShow sau khi cửa
/// sổ check-in đóng) và BR-OFF-04 (trì hoãn nếu còn dữ liệu offline chưa đối soát).
/// </summary>
public class RunNoShowFinalizationCommandHandlerTests
{
    private static RunNoShowFinalizationCommandHandler CreateHandler(
        ApplicationDbContext context, DateTimeOffset now, int graceMinutes = 15)
    {
        var clock = new Mock<IDateTimeService>();
        clock.Setup(x => x.Now).Returns(now);

        return new RunNoShowFinalizationCommandHandler(
            context, clock.Object,
            Options.Create(new NoShowFinalizationOptions { GraceMinutes = graceMinutes }),
            NullLogger<RunNoShowFinalizationCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_WhenWindowClosedAndGraceElapsed_ShouldFinalizeNotCheckedInAsNoShow()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var staffId = Guid.NewGuid();
        var (_, _, ticket, boardingCall, _) = SeedCheckInScenario(context, staffId, withAssignment: false);

        // CheckInCloseAt = departure - 5p; grace 15p -> đủ điều kiện từ departure + 10p.
        var now = boardingCall.CheckInCloseAt!.Value.AddMinutes(16);
        var handler = CreateHandler(context, now);

        var result = await handler.Handle(new RunNoShowFinalizationCommand(), CancellationToken.None);

        result.TicketsFinalized.Should().Be(1);
        var updated = context.Tickets.Single(t => t.Id == ticket.Id);
        updated.BoardingStatus.Should().Be(BoardingStatus.NoShow);
        updated.NoShowFinalizedAt.Should().Be(now);
    }

    [Fact]
    public async Task Handle_WhenStillWithinGracePeriod_ShouldNotFinalizeYet()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var staffId = Guid.NewGuid();
        var (_, _, ticket, boardingCall, _) = SeedCheckInScenario(context, staffId, withAssignment: false);

        // Cửa sổ đã đóng (now > CheckInCloseAt) nhưng chưa qua hết 15p chờ đối soát.
        var now = boardingCall.CheckInCloseAt!.Value.AddMinutes(5);
        var handler = CreateHandler(context, now);

        var result = await handler.Handle(new RunNoShowFinalizationCommand(), CancellationToken.None);

        result.TicketsFinalized.Should().Be(0);
        context.Tickets.Single(t => t.Id == ticket.Id).BoardingStatus.Should().Be(BoardingStatus.NotCheckedIn);
    }

    [Fact]
    public async Task Handle_WhenAlreadyCheckedIn_ShouldNotBeTouched()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var staffId = Guid.NewGuid();
        var (_, _, ticket, boardingCall, _) = SeedCheckInScenario(context, staffId, withAssignment: false);

        ticket.BoardingStatus = BoardingStatus.CheckedIn;
        ticket.CheckedInAt = boardingCall.CheckInCloseAt!.Value.AddMinutes(-1);
        context.SaveChanges();

        var now = boardingCall.CheckInCloseAt!.Value.AddMinutes(16);
        var handler = CreateHandler(context, now);

        var result = await handler.Handle(new RunNoShowFinalizationCommand(), CancellationToken.None);

        result.TicketsFinalized.Should().Be(0);
        context.Tickets.Single(t => t.Id == ticket.Id).BoardingStatus.Should().Be(BoardingStatus.CheckedIn);
    }

    [Fact]
    public async Task Handle_WhenStopCallHasUnresolvedOfflineEvent_ShouldDeferFinalization()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var staffId = Guid.NewGuid();
        var (_, _, ticket, boardingCall, _) = SeedCheckInScenario(context, staffId, withAssignment: false);

        context.CheckInEvents.Add(new CheckInEvent
        {
            TicketId = ticket.Id, StaffAccountId = staffId, TripStopCallId = boardingCall.Id,
            ClientEventId = "evt-pending", OccurredAtDevice = DateTimeOffset.UtcNow,
            Outcome = "Success", SyncStatus = "Pending"
        });
        context.SaveChanges();

        var now = boardingCall.CheckInCloseAt!.Value.AddMinutes(16);
        var handler = CreateHandler(context, now);

        var result = await handler.Handle(new RunNoShowFinalizationCommand(), CancellationToken.None);

        result.TicketsFinalized.Should().Be(0);
        result.StopCallsDeferred.Should().Be(1);
        context.Tickets.Single(t => t.Id == ticket.Id).BoardingStatus.Should().Be(BoardingStatus.NotCheckedIn);
    }

    [Theory]
    [InlineData(TripStatus.Cancelled)]
    [InlineData(TripStatus.Terminated)]
    public async Task Handle_WhenTripCancelledOrTerminatedBeforeBoarding_ShouldNotFinalizeAsNoShow(TripStatus tripStatus)
    {
        // H02/BR-RF-06: chuyến hủy/chấm dứt trước khi khách có cơ hội lên tàu không được kết luận
        // NoShow, nếu không khách sẽ mất quyền hoàn tiền (RefundEligibilityEvaluator từ chối NoShow).
        using var context = TestDbContextFactory.CreateDbContext();
        var staffId = Guid.NewGuid();
        var (seed, _, ticket, boardingCall, _) = SeedCheckInScenario(context, staffId, withAssignment: false);
        seed.Trip.Status = tripStatus;
        context.SaveChanges();

        var now = boardingCall.CheckInCloseAt!.Value.AddMinutes(16);
        var handler = CreateHandler(context, now);

        var result = await handler.Handle(new RunNoShowFinalizationCommand(), CancellationToken.None);

        result.TicketsFinalized.Should().Be(0);
        context.Tickets.Single(t => t.Id == ticket.Id).BoardingStatus.Should().Be(BoardingStatus.NotCheckedIn);
    }
}
