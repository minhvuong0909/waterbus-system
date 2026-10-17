using FluentAssertions;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Rules;
using Xunit;

namespace WaterbusSystem.UnitTests.Trips;

/// <summary>BR-OPS-01..03: vòng đời trạng thái chuyến.</summary>
public class TripLifecycleTests
{
    private static Trip TripIn(TripStatus status, bool started = false) =>
        new() { Status = status, ActualDepartureTime = started ? DateTimeOffset.UtcNow : null };

    [Theory]
    [InlineData(TripStatus.Scheduled, TripOperation.OpenBoarding, TripStatus.Boarding)]
    [InlineData(TripStatus.Scheduled, TripOperation.Start, TripStatus.EnRoute)]
    [InlineData(TripStatus.Boarding, TripOperation.Start, TripStatus.EnRoute)]
    [InlineData(TripStatus.EnRoute, TripOperation.Arrive, TripStatus.Arrived)]
    [InlineData(TripStatus.Arrived, TripOperation.Complete, TripStatus.Completed)]
    [InlineData(TripStatus.Scheduled, TripOperation.Suspend, TripStatus.Suspended)]
    [InlineData(TripStatus.Boarding, TripOperation.Suspend, TripStatus.Suspended)]
    [InlineData(TripStatus.EnRoute, TripOperation.Suspend, TripStatus.Suspended)]
    [InlineData(TripStatus.Scheduled, TripOperation.Cancel, TripStatus.Cancelled)]
    [InlineData(TripStatus.Boarding, TripOperation.Cancel, TripStatus.Cancelled)]
    [InlineData(TripStatus.EnRoute, TripOperation.Terminate, TripStatus.Terminated)]
    public void NextStatus_ValidTransition_ShouldReturnTarget(TripStatus from, TripOperation op, TripStatus expected) =>
        TripLifecycle.NextStatus(TripIn(from), op).Should().Be(expected);

    [Theory]
    [InlineData(TripStatus.EnRoute, TripOperation.Cancel)]        // đã chạy: phải Terminate, không Cancel (BR-OPS-03)
    [InlineData(TripStatus.Scheduled, TripOperation.Terminate)]   // chưa chạy: phải Cancel
    [InlineData(TripStatus.Scheduled, TripOperation.Arrive)]
    [InlineData(TripStatus.Boarding, TripOperation.Arrive)]
    [InlineData(TripStatus.Scheduled, TripOperation.Complete)]
    [InlineData(TripStatus.EnRoute, TripOperation.Start)]
    [InlineData(TripStatus.EnRoute, TripOperation.OpenBoarding)]
    [InlineData(TripStatus.Scheduled, TripOperation.Resume)]
    [InlineData(TripStatus.Suspended, TripOperation.Suspend)]
    [InlineData(TripStatus.Suspended, TripOperation.Start)]
    public void NextStatus_InvalidTransition_ShouldReturnNull(TripStatus from, TripOperation op) =>
        TripLifecycle.NextStatus(TripIn(from), op).Should().BeNull();

    [Theory]
    [InlineData(TripStatus.Completed)]
    [InlineData(TripStatus.Cancelled)]
    [InlineData(TripStatus.Terminated)]
    public void FinishedTrips_ShouldAllowNothing(TripStatus status) =>
        TripLifecycle.AllowedOperations(TripIn(status, started: true)).Should().BeEmpty();

    [Fact]
    public void Resume_SuspendedBeforeDeparture_ShouldReturnToScheduled() =>
        TripLifecycle.NextStatus(TripIn(TripStatus.Suspended, started: false), TripOperation.Resume).Should().Be(TripStatus.Scheduled);

    [Fact]
    public void Resume_SuspendedAfterDeparture_ShouldReturnToEnRoute() =>
        TripLifecycle.NextStatus(TripIn(TripStatus.Suspended, started: true), TripOperation.Resume).Should().Be(TripStatus.EnRoute);

    [Fact]
    public void Suspended_NotStarted_CanCancelButNotTerminate()
    {
        var trip = TripIn(TripStatus.Suspended, started: false);

        TripLifecycle.NextStatus(trip, TripOperation.Cancel).Should().Be(TripStatus.Cancelled);
        TripLifecycle.NextStatus(trip, TripOperation.Terminate).Should().BeNull();
    }

    [Fact]
    public void Suspended_AlreadyStarted_CanTerminateButNotCancel()
    {
        var trip = TripIn(TripStatus.Suspended, started: true);

        TripLifecycle.NextStatus(trip, TripOperation.Terminate).Should().Be(TripStatus.Terminated);
        TripLifecycle.NextStatus(trip, TripOperation.Cancel).Should().BeNull();
    }

    [Fact]
    public void AllowedOperations_ShouldNeverIncludeNoAction() =>
        Enum.GetValues<TripStatus>().SelectMany(s => TripLifecycle.AllowedOperations(TripIn(s, true)))
            .Should().NotContain(TripOperation.NoAction);

    [Fact]
    public void AllowedOperations_ForEnRoute_ShouldBeArriveSuspendTerminate() =>
        TripLifecycle.AllowedOperations(TripIn(TripStatus.EnRoute, true))
            .Should().BeEquivalentTo([TripOperation.Arrive, TripOperation.Suspend, TripOperation.Terminate]);

    [Theory]
    [InlineData(TripOperation.OpenBoarding, true)]
    [InlineData(TripOperation.Start, true)]
    [InlineData(TripOperation.Arrive, true)]
    [InlineData(TripOperation.Suspend, false)]
    [InlineData(TripOperation.Resume, false)]
    [InlineData(TripOperation.Cancel, false)]
    [InlineData(TripOperation.Terminate, false)]
    [InlineData(TripOperation.Complete, false)]
    public void IsCaptainOperation_ShouldMatchBrOps04(TripOperation op, bool expected) =>
        TripLifecycle.IsCaptainOperation(op).Should().Be(expected);

    [Theory]
    [InlineData(TripOperation.Suspend)]
    [InlineData(TripOperation.Cancel)]
    [InlineData(TripOperation.Terminate)]
    [InlineData(TripOperation.NoAction)]
    public void RequiresReason_ForDecisions(TripOperation op) => TripLifecycle.RequiresReason(op).Should().BeTrue();

    [Fact]
    public void DecisionOf_NoAction_ShouldBeNone() => TripLifecycle.DecisionOf(TripOperation.NoAction).Should().Be("None");
}
