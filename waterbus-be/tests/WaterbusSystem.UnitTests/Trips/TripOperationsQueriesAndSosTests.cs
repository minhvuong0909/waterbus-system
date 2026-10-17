using FluentAssertions;
using Moq;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Trips.Commands.ReportTripSos;
using WaterbusSystem.Application.Features.Trips.Queries.GetTripOperationEvents;
using WaterbusSystem.Application.Features.Trips.Queries.GetTripOperations;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;

namespace WaterbusSystem.UnitTests.Trips;

public class TripOperationsQueriesAndSosTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 8, 0, 0, TimeSpan.Zero);

    private readonly ApplicationDbContext _context = TestDbContextFactory.CreateDbContext();
    private readonly TestDbContextFactory.TripSeedData _seed;
    private readonly Mock<ICurrentUserService> _user = new();
    private readonly Mock<IDateTimeService> _clock = new();
    private readonly Mock<IOperationsAlertPublisher> _publisher = new();
    private readonly Guid _captainId = Guid.NewGuid();

    public TripOperationsQueriesAndSosTests()
    {
        _clock.Setup(c => c.Now).Returns(() => Now);
        _seed = TestDbContextFactory.SeedTrip(_context);
        _seed.Boat.CaptainUserId = _captainId;
        _seed.Trip.DepartureTime = Now.AddHours(1);
        _seed.Trip.ArrivalTime = Now.AddHours(2);
        _context.SaveChanges();
    }

    private void As(string role, Guid? id = null)
    {
        _user.Setup(u => u.UserId).Returns((id ?? Guid.NewGuid()).ToString());
        _user.Setup(u => u.UserRole).Returns(role);
    }

    private Trip AddTrip(Boat boat, DateTimeOffset departure, TripStatus status = TripStatus.Scheduled)
    {
        var trip = new Trip { RouteId = _seed.Route.Id, BoatId = boat.Id, DepartureTime = departure, ArrivalTime = departure.AddMinutes(45), Status = status };
        _context.Trips.Add(trip);
        _context.SaveChanges();
        return trip;
    }

    private GetTripOperationsQueryHandler OperationsHandler() => new(_context, _user.Object, _clock.Object);

    // ---- GetTripOperations ----

    [Fact]
    public async Task Operations_Admin_ShouldSeeAllBoatsInWindowOrderedByDeparture()
    {
        As("Admin");
        AddTrip(_seed.OtherBoat, Now.AddMinutes(30));
        AddTrip(_seed.Boat, Now.AddDays(10)); // ngoài khung mặc định 7 ngày

        var items = await OperationsHandler().Handle(new GetTripOperationsQuery(), CancellationToken.None);

        items.Select(i => i.DepartureTime).Should().BeInAscendingOrder();
        items.Should().HaveCount(2);
        items.First().Should().Match<TripOperationItemDto>(i => i.RouteCode == "RT01" && i.BoatCode != null && i.Status == "Scheduled");
        items.Single(i => i.Id == _seed.Trip.Id).AllowedOperations.Should().Contain(["Start", "Suspend", "Cancel"]);
    }

    [Fact]
    public async Task Operations_Captain_ShouldOnlySeeTripsOfOwnBoat()
    {
        As("Captain", _captainId);
        AddTrip(_seed.OtherBoat, Now.AddMinutes(30));

        var items = await OperationsHandler().Handle(new GetTripOperationsQuery(), CancellationToken.None);

        items.Should().ContainSingle().Which.Id.Should().Be(_seed.Trip.Id);
    }

    [Fact]
    public async Task Operations_ShouldFilterByStatus()
    {
        As("Admin");
        AddTrip(_seed.Boat, Now.AddMinutes(5), TripStatus.EnRoute);

        var items = await OperationsHandler().Handle(new GetTripOperationsQuery(Statuses: [TripStatus.EnRoute]), CancellationToken.None);

        items.Should().ContainSingle().Which.Status.Should().Be("EnRoute");
    }

    [Fact]
    public async Task Operations_Passenger_ShouldBeForbidden()
    {
        As("Passenger");

        var act = () => OperationsHandler().Handle(new GetTripOperationsQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(-5, false)]
    [InlineData(31, true)]
    [InlineData(32, false)]
    public void OperationsValidator_ShouldLimitRange(int days, bool valid) =>
        new GetTripOperationsQueryValidator().Validate(new GetTripOperationsQuery(Now, Now.AddDays(days))).IsValid.Should().Be(valid);

    // ---- GetTripOperationEvents ----

    [Fact]
    public async Task Events_ShouldBeNewestFirst_WithActorNames_AndFilterByTrip()
    {
        var adminId = Guid.NewGuid();
        var other = AddTrip(_seed.Boat, Now.AddHours(3));
        _context.TripOperationEvents.AddRange(
            new TripOperationEvent { TripId = _seed.Trip.Id, Action = "Suspend", FromStatus = "Scheduled", ToStatus = "Suspended", OccurredAt = Now.AddMinutes(-10), ActedByAdminId = adminId, Note = "Dông" },
            new TripOperationEvent { TripId = _seed.Trip.Id, Action = "Resume", FromStatus = "Suspended", ToStatus = "Scheduled", OccurredAt = Now.AddMinutes(-5), ActedByCaptainId = _captainId },
            new TripOperationEvent { TripId = other.Id, Action = "Cancel", FromStatus = "Scheduled", ToStatus = "Cancelled", OccurredAt = Now.AddMinutes(-1) });
        _context.SaveChanges();
        var users = new Mock<IUserDirectory>();
        users.Setup(u => u.GetFullNamesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [adminId] = "Admin Hệ Thống", [_captainId] = "Thuyền Trưởng" });
        var handler = new GetTripOperationEventsQueryHandler(_context, users.Object);

        var all = await handler.Handle(new GetTripOperationEventsQuery(), CancellationToken.None);
        var forTrip = await handler.Handle(new GetTripOperationEventsQuery(_seed.Trip.Id), CancellationToken.None);

        all.Select(e => e.Action).Should().Equal("Cancel", "Resume", "Suspend");
        all.First().ActedBy.Should().BeNull("sự kiện không có người thực hiện");
        forTrip.Select(e => (e.Action, e.ActedBy, e.Note)).Should().Equal(("Resume", "Thuyền Trưởng", null), ("Suspend", "Admin Hệ Thống", "Dông"));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(200, true)]
    [InlineData(201, false)]
    public void EventsValidator_ShouldLimitTake(int take, bool valid) =>
        new GetTripOperationEventsQueryValidator().Validate(new GetTripOperationEventsQuery(null, take)).IsValid.Should().Be(valid);

    // ---- ReportTripSos ----

    private ReportTripSosCommandHandler SosHandler() => new(_context, _user.Object, _clock.Object, _publisher.Object);

    [Fact]
    public async Task Sos_ByAssignedCaptain_ShouldCreateHighSafetyIncident_NotChangeTrip_AndNotifyAdmins()
    {
        As("Captain", _captainId);

        var report = await SosHandler().Handle(new ReportTripSosCommand(_seed.Trip.Id, "Cháy động cơ"), CancellationToken.None);

        var incident = _context.Incidents.Single();
        incident.Id.Should().Be(report.IncidentId);
        (incident.Source, incident.IncidentType, incident.Severity, incident.Status, incident.ScopeType)
            .Should().Be(("Captain", "Safety", "High", "Open", "Trip"));
        (incident.ReportedByCaptainId, incident.BoatId, incident.Description).Should().Be(((Guid?)_captainId, (Guid?)_seed.Boat.Id, "Cháy động cơ"));
        _context.IncidentTrips.Should().ContainSingle(l => l.TripId == _seed.Trip.Id && l.OperationDecision == null);
        _context.Trips.Find(_seed.Trip.Id)!.Status.Should().Be(TripStatus.Scheduled, "BR-INC-04: ghi nhận sự cố không tự đổi trạng thái chuyến");
        _publisher.Verify(p => p.PublishIncidentRaisedAsync(
            It.Is<IncidentRaisedMessage>(m => m.IncidentId == report.IncidentId && m.TripId == _seed.Trip.Id && m.Severity == "High"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Sos_WithoutNote_ShouldUseDefaultText()
    {
        As("Captain", _captainId);

        await SosHandler().Handle(new ReportTripSosCommand(_seed.Trip.Id, "  "), CancellationToken.None);

        _context.Incidents.Single().Description.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Sos_ByCaptainOfAnotherBoat_ShouldBeForbidden()
    {
        As("Captain", Guid.NewGuid());

        var act = () => SosHandler().Handle(new ReportTripSosCommand(_seed.Trip.Id, "x"), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
        _context.Incidents.Should().BeEmpty();
    }

    [Fact]
    public async Task Sos_ByAdmin_ShouldBeForbidden()
    {
        As("Admin");

        var act = () => SosHandler().Handle(new ReportTripSosCommand(_seed.Trip.Id, "x"), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task Sos_UnknownTrip_ShouldThrowNotFound()
    {
        As("Captain", _captainId);

        var act = () => SosHandler().Handle(new ReportTripSosCommand(Guid.NewGuid(), "x"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
