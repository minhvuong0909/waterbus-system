using FluentAssertions;
using Moq;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Trips.Commands.ChangeTripStatus;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;

namespace WaterbusSystem.UnitTests.Trips;

public class ChangeTripStatusCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 8, 0, 0, TimeSpan.Zero);

    private readonly ApplicationDbContext _context = TestDbContextFactory.CreateDbContext();
    private readonly TestDbContextFactory.TripSeedData _seed;
    private readonly Mock<ICurrentUserService> _user = new();
    private readonly Mock<IDateTimeService> _clock = new();
    private readonly Mock<IOperationsAlertPublisher> _publisher = new();
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly Guid _captainId = Guid.NewGuid();

    public ChangeTripStatusCommandHandlerTests()
    {
        _clock.Setup(c => c.Now).Returns(() => Now);
        _seed = TestDbContextFactory.SeedTrip(_context);
        _seed.Boat.CaptainUserId = _captainId;
        _context.SaveChanges();
    }

    private ChangeTripStatusCommandHandler Handler() => new(_context, _user.Object, _clock.Object, _publisher.Object);

    private void AsAdmin() { _user.Setup(u => u.UserId).Returns(_adminId.ToString()); _user.Setup(u => u.UserRole).Returns("Admin"); }
    private void AsCaptain(Guid? id = null) { _user.Setup(u => u.UserId).Returns((id ?? _captainId).ToString()); _user.Setup(u => u.UserRole).Returns("Captain"); }

    private ChangeTripStatusCommand Cmd(TripOperation op, string? reason = null, Guid? incidentId = null) =>
        new(_seed.Trip.Id, op, reason, incidentId);

    private Incident AddIncident(bool linkTrip)
    {
        var incident = new Incident { ScopeType = "Route", Source = "WeatherService", IncidentType = "Weather", Status = "Open" };
        _context.Incidents.Add(incident);
        if (linkTrip) _context.IncidentTrips.Add(new IncidentTrip { IncidentId = incident.Id, TripId = _seed.Trip.Id });
        _context.SaveChanges();
        return incident;
    }

    [Fact]
    public async Task Captain_StartsOwnTrip_ShouldMoveToEnRouteStampDepartureLogAndPublish()
    {
        AsCaptain();

        var result = await Handler().Handle(Cmd(TripOperation.Start), CancellationToken.None);

        result.Status.Should().Be("EnRoute");
        result.PreviousStatus.Should().Be("Scheduled");
        result.ActualDepartureTime.Should().Be(Now);
        result.AllowedOperations.Should().BeEquivalentTo("Arrive", "Suspend", "Terminate");
        _context.Trips.Find(_seed.Trip.Id)!.Status.Should().Be(TripStatus.EnRoute);
        var log = _context.TripOperationEvents.Single();
        (log.Action, log.FromStatus, log.ToStatus, log.ActedByCaptainId, log.ActedByAdminId)
            .Should().Be(("Start", "Scheduled", "EnRoute", _captainId, (Guid?)null));
        _publisher.Verify(p => p.PublishTripStatusChangedAsync(
            It.Is<TripStatusChangedMessage>(m => m.TripId == _seed.Trip.Id && m.Status == "EnRoute" && m.PreviousStatus == "Scheduled" && m.BoatId == _seed.Boat.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Start_BeforeSalesClose_ShouldCloseSalesAtDeparture()
    {
        // Rời bến sớm hơn giờ đóng bán đã đặt: nếu không đóng bán thì vi phạm CK_Trips_SalesClose
        AsCaptain();
        _seed.Trip.DepartureTime = Now.AddHours(1);
        _seed.Trip.SalesCloseAt = Now.AddMinutes(45);
        _context.SaveChanges();

        await Handler().Handle(Cmd(TripOperation.Start), CancellationToken.None);

        _context.Trips.Find(_seed.Trip.Id)!.SalesCloseAt.Should().Be(Now);
    }

    [Fact]
    public async Task Start_WithoutSalesClose_ShouldCloseSalesNoLaterThanPlannedDeparture()
    {
        AsCaptain();
        _seed.Trip.DepartureTime = Now.AddMinutes(-20); // rời bến trễ
        _seed.Trip.SalesCloseAt = null;
        _context.SaveChanges();

        await Handler().Handle(Cmd(TripOperation.Start), CancellationToken.None);

        _context.Trips.Find(_seed.Trip.Id)!.SalesCloseAt.Should().Be(Now.AddMinutes(-20));
    }

    [Fact]
    public async Task Start_WhenSalesAlreadyClosedEarlier_ShouldKeepTheEarlierTime()
    {
        AsCaptain();
        _seed.Trip.DepartureTime = Now.AddMinutes(-5);
        _seed.Trip.SalesCloseAt = Now.AddMinutes(-30);
        _context.SaveChanges();

        await Handler().Handle(Cmd(TripOperation.Start), CancellationToken.None);

        _context.Trips.Find(_seed.Trip.Id)!.SalesCloseAt.Should().Be(Now.AddMinutes(-30));
    }

    [Fact]
    public async Task Captain_ArrivesAfterStart_ShouldStampArrival()
    {
        AsCaptain();
        await Handler().Handle(Cmd(TripOperation.Start), CancellationToken.None);

        var result = await Handler().Handle(Cmd(TripOperation.Arrive), CancellationToken.None);

        result.Status.Should().Be("Arrived");
        result.ActualArrivalTime.Should().Be(Now);
    }

    [Theory]
    [InlineData(TripOperation.Suspend)]
    [InlineData(TripOperation.Cancel)]
    [InlineData(TripOperation.Terminate)]
    [InlineData(TripOperation.Complete)]
    [InlineData(TripOperation.Resume)]
    public async Task Captain_DecisionOperations_ShouldBeForbidden(TripOperation op)
    {
        AsCaptain();

        var act = () => Handler().Handle(Cmd(op, "x"), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
        _context.Trips.Find(_seed.Trip.Id)!.Status.Should().Be(TripStatus.Scheduled);
        _context.TripOperationEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task Captain_OfAnotherBoat_ShouldBeForbidden()
    {
        AsCaptain(Guid.NewGuid());

        var act = () => Handler().Handle(Cmd(TripOperation.Start), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task Captain_CannotAttachAnIncident()
    {
        AsCaptain();
        var incident = AddIncident(linkTrip: true);

        var act = () => Handler().Handle(Cmd(TripOperation.Start, null, incident.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Passenger")]
    public async Task OtherRoles_ShouldBeForbidden(string? role)
    {
        _user.Setup(u => u.UserId).Returns(Guid.NewGuid().ToString());
        _user.Setup(u => u.UserRole).Returns(role);

        var act = () => Handler().Handle(Cmd(TripOperation.Start), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task Admin_InvalidTransition_ShouldThrowConflictAndChangeNothing()
    {
        AsAdmin();

        var act = () => Handler().Handle(Cmd(TripOperation.Terminate, "x"), CancellationToken.None);

        (await act.Should().ThrowAsync<ConflictException>()).Which.Message.Should().Contain("Scheduled");
        _context.TripOperationEvents.Should().BeEmpty();
        _publisher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Admin_StartWithoutBoat_ShouldThrowConflict()
    {
        AsAdmin();
        _seed.Trip.BoatId = null;
        _context.SaveChanges();

        var act = () => Handler().Handle(Cmd(TripOperation.Start), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task UnknownTrip_ShouldThrowNotFound()
    {
        AsAdmin();

        var act = () => Handler().Handle(new ChangeTripStatusCommand(Guid.NewGuid(), TripOperation.Cancel, "x", null), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Admin_CancelsTripForIncident_ShouldRecordDecisionAndHandler()
    {
        AsAdmin();
        var incident = AddIncident(linkTrip: true);

        var result = await Handler().Handle(Cmd(TripOperation.Cancel, "Gió mạnh", incident.Id), CancellationToken.None);

        result.Status.Should().Be("Cancelled");
        var link = _context.IncidentTrips.Single();
        (link.OperationDecision, link.DecisionByAdminId, link.DecisionAt).Should().Be(("Cancel", (Guid?)_adminId, (DateTimeOffset?)Now));
        _context.Incidents.Single().HandledByAdminId.Should().Be(_adminId);
        var log = _context.TripOperationEvents.Single();
        (log.IncidentId, log.Note, log.ActedByAdminId).Should().Be((incident.Id, "Gió mạnh", (Guid?)_adminId));
    }

    [Fact]
    public async Task Admin_DecidingAboutUnlinkedTrip_ShouldCreateTheLink()
    {
        AsAdmin();
        var incident = AddIncident(linkTrip: false);

        await Handler().Handle(Cmd(TripOperation.Suspend, "Dông", incident.Id), CancellationToken.None);

        _context.IncidentTrips.Should().ContainSingle(l => l.IncidentId == incident.Id && l.TripId == _seed.Trip.Id && l.OperationDecision == "Suspend");
    }

    [Fact]
    public async Task Admin_UnknownIncident_ShouldThrowNotFound()
    {
        AsAdmin();

        var act = () => Handler().Handle(Cmd(TripOperation.Suspend, "x", Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        _context.Trips.Find(_seed.Trip.Id)!.Status.Should().Be(TripStatus.Scheduled);
    }

    [Fact]
    public async Task Admin_NoAction_ShouldRecordDecisionWithoutChangingTripOrLogging()
    {
        AsAdmin();
        var incident = AddIncident(linkTrip: true);

        var result = await Handler().Handle(Cmd(TripOperation.NoAction, "Gió đã giảm", incident.Id), CancellationToken.None);

        result.Status.Should().Be("Scheduled");
        _context.IncidentTrips.Single().OperationDecision.Should().Be("None");
        _context.TripOperationEvents.Should().BeEmpty("không đổi TripStatus nên không có audit đổi trạng thái");
        _publisher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Admin_SuspendThenResumeBeforeDeparture_ShouldReturnToScheduled()
    {
        AsAdmin();
        await Handler().Handle(Cmd(TripOperation.Suspend, "Dông"), CancellationToken.None);

        var result = await Handler().Handle(Cmd(TripOperation.Resume), CancellationToken.None);

        result.Status.Should().Be("Scheduled");
        _context.TripOperationEvents.Select(e => e.ToStatus).Should().BeEquivalentTo("Suspended", "Scheduled");
    }

    [Fact]
    public async Task Admin_TerminatesRunningTrip_ShouldBecomeTerminatedWithoutDepartureChange()
    {
        AsAdmin();
        await Handler().Handle(Cmd(TripOperation.Start), CancellationToken.None);

        var result = await Handler().Handle(Cmd(TripOperation.Terminate, "Sự cố"), CancellationToken.None);

        result.Status.Should().Be("Terminated");
        result.AllowedOperations.Should().BeEmpty();
        result.ActualArrivalTime.Should().BeNull();
    }

    [Theory]
    [InlineData(TripOperation.Suspend)]
    [InlineData(TripOperation.Cancel)]
    [InlineData(TripOperation.Terminate)]
    [InlineData(TripOperation.NoAction)]
    public void Validator_ReasonRequiredForDecisions(TripOperation op)
    {
        var validator = new ChangeTripStatusCommandValidator();

        validator.Validate(new ChangeTripStatusCommand(Guid.NewGuid(), op, " ", Guid.NewGuid())).IsValid.Should().BeFalse();
        validator.Validate(new ChangeTripStatusCommand(Guid.NewGuid(), op, "Lý do", Guid.NewGuid())).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_NoActionNeedsIncident_AndReasonIsLimited()
    {
        var validator = new ChangeTripStatusCommandValidator();

        validator.Validate(new ChangeTripStatusCommand(Guid.NewGuid(), TripOperation.NoAction, "x", null)).IsValid.Should().BeFalse();
        validator.Validate(new ChangeTripStatusCommand(Guid.NewGuid(), TripOperation.Start, new string('x', 501), null)).IsValid.Should().BeFalse();
        validator.Validate(new ChangeTripStatusCommand(Guid.NewGuid(), TripOperation.Start, null, null)).IsValid.Should().BeTrue();
    }
}
