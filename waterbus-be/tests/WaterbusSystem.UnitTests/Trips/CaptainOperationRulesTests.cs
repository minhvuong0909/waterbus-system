using FluentAssertions;
using WaterbusSystem.Application.Features.Trips;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using Xunit;

namespace WaterbusSystem.UnitTests.Trips;

public class CaptainOperationRulesTests
{
    private static readonly DateTimeOffset Departure = new(2026, 11, 20, 8, 0, 0, TimeSpan.Zero);
    private static readonly CaptainOperationOptions Strict = new();

    private static Trip TripIn(TripStatus status) => new() { Status = status, DepartureTime = Departure, ArrivalTime = Departure.AddMinutes(45) };

    private static CaptainActionHint Hint(Trip trip, TripOperation op, DateTimeOffset now, double? meters = null, CaptainOperationOptions? options = null) =>
        CaptainOperationRules.Evaluate(trip, op, now, meters, options ?? Strict);

    [Fact]
    public void OpenBoarding_IsBlockedUntilTheLeadTime_AndOpensAtDepartureMinusLead()
    {
        var early = Hint(TripIn(TripStatus.Scheduled), TripOperation.OpenBoarding, Departure.AddMinutes(-31));
        var onTime = Hint(TripIn(TripStatus.Scheduled), TripOperation.OpenBoarding, Departure.AddMinutes(-30));

        early.Allowed.Should().BeFalse();
        early.Reason.Should().Be("NotYet");
        early.AvailableAt.Should().Be(Departure.AddMinutes(-30));
        onTime.Allowed.Should().BeTrue();
    }

    [Fact]
    public void Start_NeedsBoarding_ScheduledMustOpenBoardingFirst()
    {
        var scheduled = Hint(TripIn(TripStatus.Scheduled), TripOperation.Start, Departure);
        var boarding = Hint(TripIn(TripStatus.Boarding), TripOperation.Start, Departure);

        scheduled.Allowed.Should().BeFalse();
        scheduled.Reason.Should().Be("OpenBoardingFirst");
        boarding.Allowed.Should().BeTrue();
    }

    [Theory]
    [InlineData(null, false, "NoPosition")]
    [InlineData(151.0, false, "NotAtLastStop")]
    [InlineData(150.0, true, null)]
    [InlineData(0.0, true, null)]
    public void Arrive_RequiresTheBoatNearTheLastStop(double? meters, bool allowed, string? reason)
    {
        var hint = Hint(TripIn(TripStatus.EnRoute), TripOperation.Arrive, Departure.AddMinutes(40), meters);

        hint.Allowed.Should().Be(allowed);
        hint.Reason.Should().Be(reason);
    }

    [Fact]
    public void Complete_IsOnlyForAnArrivedTrip()
    {
        Hint(TripIn(TripStatus.EnRoute), TripOperation.Complete, Departure).Allowed.Should().BeFalse();
        Hint(TripIn(TripStatus.Arrived), TripOperation.Complete, Departure).Allowed.Should().BeTrue();
    }

    [Fact]
    public void WrongStatus_IsAlwaysRefused_EvenWhenNotEnforcing()
    {
        var relaxed = new CaptainOperationOptions { Enforce = false };

        Hint(TripIn(TripStatus.EnRoute), TripOperation.OpenBoarding, Departure, null, relaxed).Reason.Should().Be("WrongStatus");
        Hint(TripIn(TripStatus.Scheduled), TripOperation.OpenBoarding, Departure.AddDays(-5), null, relaxed).Allowed.Should().BeTrue();
        Hint(TripIn(TripStatus.EnRoute), TripOperation.Arrive, Departure, null, relaxed).Allowed.Should().BeTrue();
    }
}
