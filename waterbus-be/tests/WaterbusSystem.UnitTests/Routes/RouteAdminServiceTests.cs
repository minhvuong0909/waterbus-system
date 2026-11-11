using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.DTOs.Requests.Routes;
using WaterbusSystem.Application.Features.Routes.Services;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.Infrastructure.Repositories;
using WaterbusSystem.UnitTests.TestHelpers;
using WaterbusSystem.WebApi.Controllers;
using Xunit;
using ValidationException = WaterbusSystem.Application.Common.Exceptions.ValidationException;

namespace WaterbusSystem.UnitTests.Routes;

public class RouteAdminServiceTests
{
    private readonly ApplicationDbContext _context = TestDbContextFactory.CreateDbContext();
    private readonly TestDbContextFactory.TripSeedData _seed;

    public RouteAdminServiceTests() => _seed = TestDbContextFactory.SeedTrip(_context);

    private RouteAdminService Service() =>
        new(new RouteAdminRepository(_context), new EfUnitOfWork(_context), new DirectCommercialTransactionRunner());

    private static RouteStopInput Stop(Guid stationId, bool board = true, bool alight = true) => new(stationId, board, alight);

    private CreateRouteRequest NewRoute(string code, params RouteStopInput[] stops) =>
        new(code, "Tuyen moi", "Regular", 40, 7.5m, stops.ToList());

    private Task<Guid> CreateAbc(string code = "RT90") => Service().CreateAsync(
        NewRoute(code, Stop(_seed.StationA.Id), Stop(_seed.StationB.Id), Stop(_seed.StationC.Id)));

    [Fact]
    public async Task Create_ShouldNumberStopsAndDeriveEndStations()
    {
        var id = await CreateAbc();

        var detail = await Service().GetByIdAsync(id);

        detail.Stops.Select(s => s.SequenceNo).Should().Equal(1, 2, 3);
        detail.Stops.Select(s => s.StationId).Should().Equal(_seed.StationA.Id, _seed.StationB.Id, _seed.StationC.Id);
        var route = _context.Routes.Single(r => r.Id == id);
        route.DepartureStationId.Should().Be(_seed.StationA.Id);
        route.ArrivalStationId.Should().Be(_seed.StationC.Id);
        detail.StructureLocked.Should().BeFalse();
    }

    [Fact]
    public async Task Create_CircularRouteWithRepeatedNonAdjacentStation_ShouldBeAllowed()
    {
        var id = await Service().CreateAsync(NewRoute("RT91", Stop(_seed.StationA.Id), Stop(_seed.StationB.Id), Stop(_seed.StationA.Id)));

        (await Service().GetByIdAsync(id)).Stops.Should().HaveCount(3);
    }

    [Fact]
    public async Task Sightseeing_SingleBerthLoop_ShouldBeAllowedOnCreateAndReplace_ButNotForRegular()
    {
        var loop = new CreateRouteRequest("RT70", "Vong", "Sightseeing", 60, 5m, [Stop(_seed.StationA.Id), Stop(_seed.StationA.Id)]);
        var id = await Service().CreateAsync(loop);
        (await Service().GetByIdAsync(id)).Stops.Should().HaveCount(2);

        var replaced = await Service().ReplaceStopsAsync(id, new ReplaceRouteStopsRequest(
            [Stop(_seed.StationA.Id), Stop(_seed.StationB.Id), Stop(_seed.StationB.Id)]));
        replaced.Stops.Should().HaveCount(3);

        var regular = () => Service().CreateAsync(loop with { Code = "RT71", ServiceType = "Regular" });
        await regular.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Update_ToRegular_ShouldRejectRouteWithAdjacentSameStation()
    {
        var id = await Service().CreateAsync(new CreateRouteRequest("RT72", "Vong", "Sightseeing", 60, 5m, [Stop(_seed.StationA.Id), Stop(_seed.StationA.Id)]));

        var act = () => Service().UpdateAsync(id, new UpdateRouteRequest("Vong", "Regular", 60, 5m));

        await act.Should().ThrowAsync<ValidationException>();
        (await Service().GetByIdAsync(id)).ServiceType.Should().Be("Sightseeing");
    }

    [Fact]
    public async Task Create_ShouldRejectDuplicateCode()
    {
        await CreateAbc("RT92");

        var act = () => CreateAbc("RT92");

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Create_ShouldRejectAdjacentSameStationUnknownStationAndBadEnds()
    {
        var adjacent = () => Service().CreateAsync(NewRoute("RT93", Stop(_seed.StationA.Id), Stop(_seed.StationA.Id)));
        var unknown = () => Service().CreateAsync(NewRoute("RT94", Stop(_seed.StationA.Id), Stop(Guid.NewGuid())));
        var noBoardAtStart = () => Service().CreateAsync(NewRoute("RT95", Stop(_seed.StationA.Id, board: false), Stop(_seed.StationB.Id)));
        var noAlightAtEnd = () => Service().CreateAsync(NewRoute("RT96", Stop(_seed.StationA.Id), Stop(_seed.StationB.Id, alight: false)));

        await adjacent.Should().ThrowAsync<ValidationException>();
        await unknown.Should().ThrowAsync<ValidationException>();
        await noBoardAtStart.Should().ThrowAsync<ValidationException>();
        await noAlightAtEnd.Should().ThrowAsync<ValidationException>();
        _context.Routes.Count(r => r.Code.StartsWith("RT9")).Should().Be(0);
    }

    [Fact]
    public async Task Create_ShouldRejectInactiveStation()
    {
        _seed.StationB.IsActive = false;
        _context.SaveChanges();

        var act = () => Service().CreateAsync(NewRoute("RT97", Stop(_seed.StationA.Id), Stop(_seed.StationB.Id)));

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task ReplaceStops_OnUnreferencedRoute_ShouldShrinkSoftDeleteAndRegrowWithoutKeyCollision()
    {
        var id = await CreateAbc("RT98");

        var shrunk = await Service().ReplaceStopsAsync(id, new ReplaceRouteStopsRequest([Stop(_seed.StationA.Id), Stop(_seed.StationC.Id)]));
        shrunk.Stops.Select(s => s.StationId).Should().Equal(_seed.StationA.Id, _seed.StationC.Id);
        _context.RouteStops.IgnoreQueryFilters().Count(s => s.RouteId == id && s.IsDeleted).Should().Be(1);

        var regrown = await Service().ReplaceStopsAsync(id, new ReplaceRouteStopsRequest(
            [Stop(_seed.StationA.Id), Stop(_seed.StationB.Id), Stop(_seed.StationC.Id)]));
        regrown.Stops.Should().HaveCount(3);
        _context.RouteStops.IgnoreQueryFilters().Count(s => s.RouteId == id).Should().Be(3); // dòng cũ được dùng lại, không thêm
        _context.Routes.Single(r => r.Id == id).ArrivalStationId.Should().Be(_seed.StationC.Id);
    }

    [Fact]
    public async Task ReplaceStops_OnRouteUsedByTrip_ShouldConflict()
    {
        var act = () => Service().ReplaceStopsAsync(_seed.Route.Id, new ReplaceRouteStopsRequest([Stop(_seed.StationA.Id), Stop(_seed.StationB.Id)]));

        await act.Should().ThrowAsync<ConflictException>();
        (await Service().GetByIdAsync(_seed.Route.Id)).Stops.Should().HaveCount(3);
    }

    [Fact]
    public async Task Update_ServiceTypeOnReferencedRoute_ShouldConflict_ButRenameIsAllowed()
    {
        var change = () => Service().UpdateAsync(_seed.Route.Id, new UpdateRouteRequest("X", "Sightseeing", 30, 5));
        await change.Should().ThrowAsync<ConflictException>();

        await Service().UpdateAsync(_seed.Route.Id, new UpdateRouteRequest("Doi ten", "Regular", 35, 6));

        var detail = await Service().GetByIdAsync(_seed.Route.Id);
        detail.Name.Should().Be("Doi ten");
        detail.EstimatedDurationMinutes.Should().Be(35);
        detail.StructureLocked.Should().BeTrue();
    }

    [Fact]
    public async Task Deactivate_WithOpenTrip_ShouldConflict_AndWithoutDependentsShouldSucceed()
    {
        var blocked = () => Service().DeactivateAsync(_seed.Route.Id);
        await blocked.Should().ThrowAsync<ConflictException>();
        _context.Routes.Single(r => r.Id == _seed.Route.Id).IsActive.Should().BeTrue();

        var id = await CreateAbc("RT99");
        await Service().DeactivateAsync(id);
        _context.Routes.Single(r => r.Id == id).IsActive.Should().BeFalse();
        await Service().ActivateAsync(id);
        _context.Routes.Single(r => r.Id == id).IsActive.Should().BeTrue();
    }

    [Theory]
    [InlineData(TripStatus.Scheduled, true)]
    [InlineData(TripStatus.Boarding, true)]
    [InlineData(TripStatus.EnRoute, true)]
    [InlineData(TripStatus.Suspended, true)]
    [InlineData(TripStatus.Arrived, true)]
    [InlineData(TripStatus.Completed, false)]
    [InlineData(TripStatus.Cancelled, false)]
    [InlineData(TripStatus.Terminated, false)]
    public async Task Deactivate_ShouldBlockOnlyForUnclosedTrips(TripStatus status, bool blocked)
    {
        _seed.Trip.Status = status;
        _context.SaveChanges();

        var act = () => Service().DeactivateAsync(_seed.Route.Id);

        if (blocked) await act.Should().ThrowAsync<ConflictException>();
        else await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task List_ShouldFilterAndPage()
    {
        await CreateAbc("RT80");
        await Service().DeactivateAsync(await CreateAbc("RT81"));

        var active = await Service().GetPageAsync(new GetRoutesRequest(1, 10, IsActive: true));
        var all = await Service().GetPageAsync(new GetRoutesRequest(1, 2));

        active.Items.Should().NotContain(r => r.Code == "RT81");
        all.Items.Should().HaveCount(2);
        all.TotalCount.Should().BeGreaterThan(2);
        active.Items.First(r => r.Code == "RT80").StopCount.Should().Be(3);
    }

    [Fact]
    public async Task Get_UnknownRoute_ShouldBeNotFound()
    {
        var act = () => Service().GetByIdAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public void Controller_ShouldRequireAdmin()
    {
        typeof(AdminRoutesController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>()
            .Should().ContainSingle(a => a.Policy == "AdminOnly");
        typeof(AdminRoutesController).GetMethods().SelectMany(m => m.GetCustomAttributes(typeof(AllowAnonymousAttribute), true)).Should().BeEmpty();
    }

    [Fact]
    public void Validators_ShouldBoundStopsAndServiceType()
    {
        var v = new WaterbusSystem.Application.Validators.Routes.CreateRouteRequestValidator();
        var ok = NewRoute("R1", Stop(Guid.NewGuid()), Stop(Guid.NewGuid()));

        v.Validate(ok).IsValid.Should().BeTrue();
        v.Validate(ok with { Stops = [Stop(Guid.NewGuid())] }).IsValid.Should().BeFalse();
        v.Validate(ok with { ServiceType = "Ferry" }).IsValid.Should().BeFalse();
        v.Validate(ok with { Code = "" }).IsValid.Should().BeFalse();
        v.Validate(ok with { EstimatedDurationMinutes = 0 }).IsValid.Should().BeFalse();
    }
}
