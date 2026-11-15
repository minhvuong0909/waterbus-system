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

    private Incident Add(string status, string type, DateTimeOffset occurredAt, string source = "Captain")
    {
        var incident = new Incident { ScopeType = "Trip", Source = source, IncidentType = type, Status = status, OccurredAt = occurredAt };
        _context.Incidents.Add(incident);
        _context.SaveChanges();
        return incident;
    }

    [Fact]
    public async Task GetPage_ShouldFilterByStatusAndOrderNewestFirst()
    {
        var older = Add("Open", "Safety", Now.AddHours(-2));
        var newer = Add("Open", "Safety", Now.AddHours(-1));
        Add("Resolved", "Safety", Now);

        var page = await Service().GetPageAsync(new GetIncidentsRequest(Status: "Open"));

        page.TotalCount.Should().Be(2);
        page.Items.Select(i => i.Id).Should().Equal(newer.Id, older.Id);
    }

    [Fact]
    public async Task GetPage_ShouldHideLegacyWeatherIncidents_ButKeepThemStoredAndReadableById()
    {
        // Giám sát thời tiết đã gỡ: Incident cũ của WeatherService còn Open không được làm bẩn danh sách đang hoạt động.
        var sos = Add("Open", "Safety", Now);
        var legacy = Add("Open", "Weather", Now.AddMinutes(1), source: "WeatherService");

        var open = await Service().GetPageAsync(new GetIncidentsRequest(Status: "Open"));
        open.Items.Select(i => i.Id).Should().Equal(sos.Id);
        open.TotalCount.Should().Be(1);

        (await Service().GetPageAsync(new GetIncidentsRequest())).Items.Select(i => i.Id).Should().NotContain(legacy.Id);

        // Không xoá dữ liệu lịch sử: còn trong CSDL, xem được theo Id và khi chủ động lọc đúng loại lịch sử.
        _context.Incidents.Any(i => i.Id == legacy.Id).Should().BeTrue();
        (await Service().GetByIdAsync(legacy.Id)).Source.Should().Be("WeatherService");
        (await Service().GetPageAsync(new GetIncidentsRequest(IncidentType: "Weather"))).Items
            .Select(i => i.Id).Should().Equal(legacy.Id);
    }

    [Fact]
    public async Task GetPage_ShouldPaginate()
    {
        for (var i = 0; i < 3; i++) Add("Open", "Safety", Now.AddMinutes(i));

        var page = await Service().GetPageAsync(new GetIncidentsRequest(PageNumber: 2, PageSize: 2));

        page.Items.Should().HaveCount(1);
        page.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task GetById_ShouldReturnIncidentWithAffectedTrips()
    {
        var seed = TestDbContextFactory.SeedTrip(_context);
        var incident = Add("Open", "Safety", Now);
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
