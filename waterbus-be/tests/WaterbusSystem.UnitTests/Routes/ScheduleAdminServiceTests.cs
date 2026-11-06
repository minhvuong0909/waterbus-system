using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using WaterbusSystem.Application.DTOs.Requests.Schedules;
using WaterbusSystem.Application.Features.Schedules.Services;
using WaterbusSystem.Application.Validators.Schedules;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.Infrastructure.Repositories;
using WaterbusSystem.UnitTests.TestHelpers;
using WaterbusSystem.WebApi.Controllers;
using Xunit;
using ValidationException = WaterbusSystem.Application.Common.Exceptions.ValidationException;

namespace WaterbusSystem.UnitTests.Routes;

public class ScheduleAdminServiceTests
{
    private readonly ApplicationDbContext _context = TestDbContextFactory.CreateDbContext();
    private readonly TestDbContextFactory.TripSeedData _seed;
    private readonly List<RouteStop> _stops; // A(1), B(2), C(3) của route seed

    public ScheduleAdminServiceTests()
    {
        _seed = TestDbContextFactory.SeedTrip(_context);
        _stops = _context.RouteStops.Where(s => s.RouteId == _seed.Route.Id).OrderBy(s => s.SequenceNo).ToList();
    }

    private ScheduleAdminService Service() =>
        new(new ScheduleAdminRepository(_context), new EfUnitOfWork(_context), new DirectCommercialTransactionRunner());

    private List<ScheduleStopInput> Full() =>
        [new(_stops[0].Id, null, 0), new(_stops[1].Id, 20, 22), new(_stops[2].Id, 40, null)];

    private CreateScheduleRequest Req(string days = "1,2,3,4,5", TimeSpan? time = null, List<ScheduleStopInput>? stops = null,
        string type = "Commuter", DateOnly? from = null, DateOnly? to = null, Guid? routeId = null) =>
        new(routeId ?? _seed.Route.Id, type, "Outbound", time ?? new TimeSpan(8, 0, 0), days,
            from ?? new DateOnly(2026, 11, 1), to, stops ?? Full());

    [Fact]
    public async Task Create_ShouldNormalizeDaysAndStoreStopsInOrder()
    {
        var id = await Service().CreateAsync(Req(days: "5,1,3"));

        var detail = await Service().GetByIdAsync(id);

        detail.DaysOfWeek.Should().Be("1,3,5");
        detail.Stops.Select(s => s.VisitOrder).Should().Equal(1, 2, 3);
        detail.Stops.Select(s => s.SequenceNo).Should().Equal(1, 2, 3);
        detail.StructureLocked.Should().BeFalse();
    }

    [Fact]
    public async Task Create_ShortRunSkippingAStop_ShouldBeAllowed()
    {
        var id = await Service().CreateAsync(Req(stops: [new(_stops[0].Id, null, 0), new(_stops[2].Id, 30, null)]));

        (await Service().GetByIdAsync(id)).Stops.Should().HaveCount(2);
    }

    [Fact]
    public async Task Create_ShouldRejectReversedDuplicateOrForeignStops()
    {
        var reversed = () => Service().CreateAsync(Req(stops: [new(_stops[1].Id, null, 0), new(_stops[0].Id, 10, null)]));
        var duplicate = () => Service().CreateAsync(Req(stops: [new(_stops[0].Id, null, 0), new(_stops[0].Id, 10, null)]));
        var foreign = () => Service().CreateAsync(Req(stops: [new(_stops[0].Id, null, 0), new(Guid.NewGuid(), 10, null)]));

        await reversed.Should().ThrowAsync<ValidationException>();
        await duplicate.Should().ThrowAsync<ValidationException>();
        await foreign.Should().ThrowAsync<ValidationException>();
        _context.Schedules.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_ShouldRequireReliableStopTimes()
    {
        var missingArrival = () => Service().CreateAsync(Req(stops: [new(_stops[0].Id, null, 0), new(_stops[1].Id, null, 10), new(_stops[2].Id, 20, null)]));
        var missingDeparture = () => Service().CreateAsync(Req(stops: [new(_stops[0].Id, null, null), new(_stops[2].Id, 20, null)]));
        var firstNotZero = () => Service().CreateAsync(Req(stops: [new(_stops[0].Id, null, 5), new(_stops[2].Id, 20, null)]));
        var arrivalBeforePreviousDeparture = () => Service().CreateAsync(Req(stops: [new(_stops[0].Id, null, 0), new(_stops[1].Id, 5, 20), new(_stops[2].Id, 10, null)]));
        var arrivalAfterDeparture = () => Service().CreateAsync(Req(stops: [new(_stops[0].Id, null, 0), new(_stops[1].Id, 30, 20), new(_stops[2].Id, 40, null)]));

        await missingArrival.Should().ThrowAsync<ValidationException>();
        await missingDeparture.Should().ThrowAsync<ValidationException>();
        await firstNotZero.Should().ThrowAsync<ValidationException>();
        await arrivalBeforePreviousDeparture.Should().ThrowAsync<ValidationException>();
        await arrivalAfterDeparture.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Create_ShouldRejectTripTypeThatDoesNotMatchRouteServiceType_AndInactiveRoute()
    {
        var mismatch = () => Service().CreateAsync(Req(type: "Sightseeing"));
        await mismatch.Should().ThrowAsync<ValidationException>();

        _seed.Route.IsActive = false;
        _context.SaveChanges();
        var inactive = () => Service().CreateAsync(Req());
        await inactive.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Create_OverlappingDaysAndDates_ShouldConflict_ButDisjointDaysOrDatesAreFine()
    {
        await Service().CreateAsync(Req(days: "1,2,3", from: new DateOnly(2026, 11, 1), to: new DateOnly(2026, 12, 31)));

        var overlap = () => Service().CreateAsync(Req(days: "3,4", from: new DateOnly(2026, 12, 1)));
        await overlap.Should().ThrowAsync<ConflictException>();

        await Service().CreateAsync(Req(days: "4,5")); // ngày khác nhau
        await Service().CreateAsync(Req(days: "1,2,3", from: new DateOnly(2027, 1, 1))); // hiệu lực không giao
        await Service().CreateAsync(Req(days: "1,2,3", time: new TimeSpan(9, 0, 0))); // giờ khác
    }

    [Fact]
    public async Task Update_TripTypeWhenTripsExist_ShouldConflict_ButTimeChangeLeavesTripsUntouched()
    {
        var id = await Service().CreateAsync(Req());
        _seed.Trip.ScheduleId = id;
        _context.SaveChanges();
        var departure = _seed.Trip.DepartureTime;

        var change = () => Service().UpdateAsync(id, new UpdateScheduleRequest("Sightseeing", "Outbound", new TimeSpan(8, 0, 0), "1,2", new DateOnly(2026, 11, 1), null));
        await change.Should().ThrowAsync<ConflictException>();

        await Service().UpdateAsync(id, new UpdateScheduleRequest("Commuter", "Outbound", new TimeSpan(10, 30, 0), "1,2", new DateOnly(2026, 11, 1), null));

        (await Service().GetByIdAsync(id)).DepartureTime.Should().Be(new TimeSpan(10, 30, 0));
        _context.Trips.Single(t => t.Id == _seed.Trip.Id).DepartureTime.Should().Be(departure);
    }

    [Fact]
    public async Task ReplaceStops_ShouldBeLockedOnceTripsExist_AndOtherwiseShrinkAndRegrow()
    {
        var id = await Service().CreateAsync(Req());

        var shrunk = await Service().ReplaceStopsAsync(id, new ReplaceScheduleStopsRequest([new(_stops[0].Id, null, 0), new(_stops[2].Id, 25, null)]));
        shrunk.Stops.Should().HaveCount(2);
        var regrown = await Service().ReplaceStopsAsync(id, new ReplaceScheduleStopsRequest(Full()));
        regrown.Stops.Should().HaveCount(3);

        _seed.Trip.ScheduleId = id;
        _context.SaveChanges();
        var locked = () => Service().ReplaceStopsAsync(id, new ReplaceScheduleStopsRequest(Full()));
        await locked.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Deactivate_KeepsGeneratedTrips_AndActivateRechecksConflicts()
    {
        var first = await Service().CreateAsync(Req());
        _seed.Trip.ScheduleId = first;
        _context.SaveChanges();

        await Service().DeactivateAsync(first);
        _context.Trips.Single(t => t.Id == _seed.Trip.Id).Status.Should().Be(TripStatus.Scheduled);

        await Service().CreateAsync(Req()); // lịch trùng được phép vì lịch đầu đã ngừng
        var reactivate = () => Service().ActivateAsync(first);
        await reactivate.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task List_ShouldFilterByRoute()
    {
        await Service().CreateAsync(Req());

        (await Service().GetPageAsync(new GetSchedulesRequest(RouteId: _seed.Route.Id))).Items.Should().ContainSingle();
        (await Service().GetPageAsync(new GetSchedulesRequest(RouteId: Guid.NewGuid()))).Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Get_Unknown_ShouldBeNotFound()
    {
        var act = () => Service().GetByIdAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Theory]
    [InlineData("1,2,3", true)]
    [InlineData("7", true)]
    [InlineData("", false)]
    [InlineData("0,1", false)]
    [InlineData("1,8", false)]
    [InlineData("1,1", false)]
    [InlineData("a", false)]
    public void DaysOfWeek_ShouldAcceptOnlyDistinctIsoDays(string value, bool valid) =>
        new CreateScheduleRequestValidator().Validate(new CreateScheduleRequest(
            Guid.NewGuid(), "Commuter", "Outbound", TimeSpan.FromHours(8), value, new DateOnly(2026, 11, 1), null,
            [new(Guid.NewGuid(), null, 0), new(Guid.NewGuid(), 10, null)])).IsValid.Should().Be(valid);

    [Fact]
    public void Validator_ShouldRejectBadDirectionTimeAndDateRange()
    {
        var v = new CreateScheduleRequestValidator();
        var ok = Req();

        v.Validate(ok).IsValid.Should().BeTrue();
        v.Validate(ok with { Direction = "Sideways" }).IsValid.Should().BeFalse();
        v.Validate(ok with { DepartureTime = TimeSpan.FromHours(25) }).IsValid.Should().BeFalse();
        v.Validate(ok with { EffectiveTo = new DateOnly(2026, 10, 1) }).IsValid.Should().BeFalse();
        v.Validate(ok with { Stops = [ok.Stops[0]] }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Controller_ShouldRequireAdmin()
    {
        typeof(AdminSchedulesController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>()
            .Should().ContainSingle(a => a.Policy == "AdminOnly");
        typeof(AdminSchedulesController).GetMethods().SelectMany(m => m.GetCustomAttributes(typeof(AllowAnonymousAttribute), true)).Should().BeEmpty();
    }
}
