using FluentAssertions;
using Moq;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Incidents.Commands.ResolveIncident;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;

namespace WaterbusSystem.UnitTests.Incidents;

public class ResolveIncidentCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 18, 9, 0, 0, TimeSpan.Zero);

    private readonly ApplicationDbContext _context = TestDbContextFactory.CreateDbContext();
    private readonly Mock<ICurrentUserService> _user = new();
    private readonly Mock<IDateTimeService> _clock = new();
    private readonly Guid _adminId = Guid.NewGuid();

    public ResolveIncidentCommandHandlerTests()
    {
        _clock.Setup(c => c.Now).Returns(() => Now);
        As("Admin");
    }

    private void As(string role)
    {
        _user.Setup(u => u.UserId).Returns(_adminId.ToString());
        _user.Setup(u => u.UserRole).Returns(role);
    }

    private ResolveIncidentCommandHandler Handler() => new(_context, _user.Object, _clock.Object);

    private Incident AddIncident(string status = "Open")
    {
        var incident = new Incident { ScopeType = "Route", Source = "WeatherService", IncidentType = "Weather", Status = status, OccurredAt = Now.AddHours(-1) };
        _context.Incidents.Add(incident);
        _context.SaveChanges();
        return incident;
    }

    [Fact]
    public async Task Admin_ShouldResolveOpenIncidentWithoutTouchingTrips()
    {
        var seed = TestDbContextFactory.SeedTrip(_context);
        var incident = AddIncident();
        _context.IncidentTrips.Add(new IncidentTrip { IncidentId = incident.Id, TripId = seed.Trip.Id });
        _context.SaveChanges();

        var result = await Handler().Handle(new ResolveIncidentCommand(incident.Id), CancellationToken.None);

        result.Status.Should().Be("Resolved");
        incident.ResolvedAt.Should().Be(Now);
        incident.HandledByAdminId.Should().Be(_adminId);
        _context.Trips.Find(seed.Trip.Id)!.Status.Should().Be(TripStatus.Scheduled);
        _context.IncidentTrips.Single().OperationDecision.Should().BeNull();
    }

    [Fact]
    public async Task AlreadyResolved_ShouldThrowConflict()
    {
        var incident = AddIncident("Resolved");

        var act = () => Handler().Handle(new ResolveIncidentCommand(incident.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task UnknownIncident_ShouldThrowNotFound()
    {
        var act = () => Handler().Handle(new ResolveIncidentCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Theory]
    [InlineData("Captain")]
    [InlineData("Staff")]
    public async Task NonAdmin_ShouldBeForbidden(string role)
    {
        As(role);
        var incident = AddIncident();

        var act = () => Handler().Handle(new ResolveIncidentCommand(incident.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
        incident.Status.Should().Be("Open");
    }
}
