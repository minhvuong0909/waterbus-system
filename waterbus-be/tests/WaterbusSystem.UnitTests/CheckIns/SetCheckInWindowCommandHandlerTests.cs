using FluentAssertions;
using WaterbusSystem.Application.Features.CheckIns.Commands.SetCheckInWindow;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;

namespace WaterbusSystem.UnitTests.CheckIns;

/// <summary>
/// Test SetCheckInWindowCommandHandler đối chiếu BR-BOARD-05 (cửa sổ check-in theo từng điểm dừng) và BR-CFG-01.
/// </summary>
public class SetCheckInWindowCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenValid_ShouldPersistWindowOnStopCall()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var seed = TestDbContextFactory.SeedTrip(context);
        var stopCall = seed.Trip.TripStopCalls.Single(c => c.VisitOrder == 1);
        var handler = new SetCheckInWindowCommandHandler(context);

        var open = seed.Trip.DepartureTime.AddMinutes(-30);
        var close = seed.Trip.DepartureTime.AddMinutes(-5);

        var result = await handler.Handle(new SetCheckInWindowCommand(stopCall.Id, open, close), CancellationToken.None);

        result.CheckInOpenAt.Should().Be(open);
        result.CheckInCloseAt.Should().Be(close);

        var updated = context.TripStopCalls.Single(c => c.Id == stopCall.Id);
        updated.CheckInOpenAt.Should().Be(open);
        updated.CheckInCloseAt.Should().Be(close);
    }

    [Fact]
    public async Task Handle_WhenStopCallDoesNotExist_ShouldThrowNotFoundException()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var handler = new SetCheckInWindowCommandHandler(context);

        var act = () => handler.Handle(
            new SetCheckInWindowCommand(Guid.NewGuid(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(10)),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenStopCallAlreadyDeparted_ShouldThrowConflictException()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var seed = TestDbContextFactory.SeedTrip(context);
        var stopCall = seed.Trip.TripStopCalls.Single(c => c.VisitOrder == 1);
        stopCall.Status = "Departed";
        context.SaveChanges();

        var handler = new SetCheckInWindowCommandHandler(context);

        var act = () => handler.Handle(
            new SetCheckInWindowCommand(stopCall.Id, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(10)),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }
}
