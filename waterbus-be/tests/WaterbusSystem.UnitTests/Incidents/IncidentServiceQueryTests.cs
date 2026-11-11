using FluentAssertions;
using Moq;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Requests.Incidents;
using WaterbusSystem.Application.Features.Incidents.Services;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.Infrastructure.Repositories;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;

namespace WaterbusSystem.UnitTests.Incidents;

public class IncidentServiceQueryTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 8, 0, 0, TimeSpan.Zero);

    private readonly ApplicationDbContext _context = TestDbContextFactory.CreateDbContext();

    private IncidentService Service() =>
        new(new IncidentRepository(_context), new EfUnitOfWork(_context),
            Mock.Of<ICurrentUserService>(), Mock.Of<IDateTimeService>());

    private Incident Add(string status, string type, DateTimeOffset occurredAt)
    {
        var incident = new Incident { ScopeType = "Route", Source = "WeatherService", IncidentType = type, Status = status, OccurredAt = occurredAt };
        _context.Incidents.Add(incident);
        _context.SaveChanges();
        return incident;
    }

    [Fact]
    public async Task GetPage_ShouldFilterByStatusAndOrderNewestFirst()
    {
        var older = Add("Open", "Weather", Now.AddHours(-2));
        var newer = Add("Open", "Weather", Now.AddHours(-1));
        Add("Resolved", "Weather", Now);

        var page = await Service().GetPageAsync(new GetIncidentsRequest(Status: "Open"));

        page.TotalCount.Should().Be(2);
        page.Items.Select(i => i.Id).Should().Equal(newer.Id, older.Id);
    }

    [Fact]
    public async Task GetPage_ShouldPaginate()
    {
        for (var i = 0; i < 3; i++) Add("Open", "Weather", Now.AddMinutes(i));

        var page = await Service().GetPageAsync(new GetIncidentsRequest(PageNumber: 2, PageSize: 2));

        page.Items.Should().HaveCount(1);
        page.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task GetById_ShouldReturnIncidentWithAffectedTrips()
    {
        var seed = TestDbContextFactory.SeedTrip(_context);
        var incident = Add("Open", "Weather", Now);
        _context.IncidentTrips.Add(new IncidentTrip { IncidentId = incident.Id, TripId = seed.Trip.Id });
        _context.SaveChanges();

        var detail = await Service().GetByIdAsync(incident.Id);

        detail.Id.Should().Be(incident.Id);
        detail.AffectedTrips.Should().ContainSingle(t => t.TripId == seed.Trip.Id && t.OperationDecision == null);
    }

    [Fact]
    public async Task GetById_UnknownId_ShouldThrowNotFound()
    {
        var act = () => Service().GetByIdAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
