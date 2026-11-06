using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Moq;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Requests.Fares;
using WaterbusSystem.Application.Features.Fares.Services;
using WaterbusSystem.Application.Validators.Fares;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.Infrastructure.Repositories;
using WaterbusSystem.UnitTests.TestHelpers;
using WaterbusSystem.WebApi.Controllers;
using Xunit;
using ValidationException = WaterbusSystem.Application.Common.Exceptions.ValidationException;

namespace WaterbusSystem.UnitTests.Fares;

public class FareAdminServiceTests
{
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;
    private readonly ApplicationDbContext _context = TestDbContextFactory.CreateDbContext();
    private readonly TestDbContextFactory.TripSeedData _seed;
    private readonly Mock<IDateTimeService> _clock = new();
    private readonly Mock<ICurrentUserService> _user = new();

    public FareAdminServiceTests()
    {
        _clock.Setup(c => c.Now).Returns(() => _now);
        _user.Setup(u => u.UserId).Returns(Guid.NewGuid().ToString());
        _seed = TestDbContextFactory.SeedTrip(_context);
    }

    private FareAdminService Service() =>
        new(new FareAdminRepository(_context), new EfUnitOfWork(_context), new DirectCommercialTransactionRunner(), _clock.Object, _user.Object);

    private SeatClass NewClass(bool active = true)
    {
        var c = new SeatClass { Code = $"C{Guid.NewGuid():N}"[..8], Name = "Moi", IsActive = active };
        _context.SeatClasses.Add(c);
        _context.SaveChanges();
        return c;
    }

    private CreateFareRuleRequest Create(Guid classId, string type = "Commuter", decimal price = 12000m, int fromDays = 1, int? toDays = null) =>
        new(type, classId, price, "VND", _now.AddDays(fromDays), toDays is null ? null : _now.AddDays(toDays.Value));

    private FareRule SeedFare(string type = "Commuter") =>
        _context.FareRules.Single(f => f.TripType == type && f.SeatClassId == _seed.StandardClass.Id);

    [Fact]
    public async Task Create_ShouldStoreRuleWithAdminAndReportStatus()
    {
        var id = await Service().CreateAsync(Create(NewClass().Id, fromDays: 2));

        var detail = await Service().GetByIdAsync(id);

        detail.Status.Should().Be("Upcoming");
        detail.Currency.Should().Be("VND");
        _context.FareRules.Single(f => f.Id == id).CreatedByAdminId.Should().NotBeNull();
    }

    [Fact]
    public async Task Create_OverlappingActiveRuleOfSameScope_ShouldConflict_UntilOldRuleIsEnded()
    {
        var existing = SeedFare(); // mở, bắt đầu từ hôm qua, không có EffectiveTo

        var overlap = () => Service().CreateAsync(Create(_seed.StandardClass.Id, fromDays: 5));
        await overlap.Should().ThrowAsync<ConflictException>();

        await Service().UpdateAsync(existing.Id, new UpdateFareRuleRequest(existing.Price, existing.EffectiveFrom, _now.AddDays(5)));
        var id = await Service().CreateAsync(Create(_seed.StandardClass.Id, price: 18000m, fromDays: 5));

        (await Service().GetByIdAsync(id)).Price.Should().Be(18000m);
    }

    [Fact]
    public async Task Create_SameStartEvenIfInactive_ShouldConflict()
    {
        var classId = NewClass().Id;
        var id = await Service().CreateAsync(Create(classId));
        await Service().DeactivateAsync(id);

        var act = () => Service().CreateAsync(Create(classId));

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Create_ShouldRejectUnknownOrInactiveSeatClass()
    {
        var unknown = () => Service().CreateAsync(Create(Guid.NewGuid()));
        var inactive = () => Service().CreateAsync(Create(NewClass(active: false).Id));

        await unknown.Should().ThrowAsync<ValidationException>();
        await inactive.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Update_StartedRule_ShouldOnlyAllowEndDate_AndUpcomingRuleCanChangePrice()
    {
        var started = SeedFare();
        var price = () => Service().UpdateAsync(started.Id, new UpdateFareRuleRequest(99999m, started.EffectiveFrom, null));
        await price.Should().ThrowAsync<ConflictException>();
        var start = () => Service().UpdateAsync(started.Id, new UpdateFareRuleRequest(started.Price, started.EffectiveFrom.AddDays(1), null));
        await start.Should().ThrowAsync<ConflictException>();

        await Service().UpdateAsync(started.Id, new UpdateFareRuleRequest(started.Price, started.EffectiveFrom, _now.AddDays(30)));
        (await Service().GetByIdAsync(started.Id)).EffectiveTo.Should().Be(_now.AddDays(30));

        var upcoming = await Service().CreateAsync(Create(NewClass().Id, fromDays: 10));
        await Service().UpdateAsync(upcoming, new UpdateFareRuleRequest(14000m, _now.AddDays(11), null));
        (await Service().GetByIdAsync(upcoming)).Price.Should().Be(14000m);
    }

    [Fact]
    public async Task Update_ExtendingIntoNextRule_ShouldConflict()
    {
        var classId = NewClass().Id;
        var first = await Service().CreateAsync(Create(classId, fromDays: 1, toDays: 5));
        await Service().CreateAsync(Create(classId, fromDays: 5, toDays: 9));

        var act = () => Service().UpdateAsync(first, new UpdateFareRuleRequest(12000m, _now.AddDays(1), _now.AddDays(7)));

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task EditingOrDeactivatingFares_ShouldNeverChangeHistoricalQuotedAmounts()
    {
        var booking = TestDbContextFactory.SeedBooking(_context, _seed, from: 1, to: 2, fare: 15000m);
        _context.SeatReservations.Add(new SeatReservation
        {
            Booking = booking, BookingId = booking.Id, TripId = _seed.Trip.Id, SeatId = _seed.Seat1.Id,
            BoardingStopOrder = 1, DisembarkingStopOrder = 2, QuotedFare = 15000m, PaidAllocation = 15000m
        });
        _context.SaveChanges();
        var fare = SeedFare();

        await Service().UpdateAsync(fare.Id, new UpdateFareRuleRequest(fare.Price, fare.EffectiveFrom, _now.AddDays(1)));
        await Service().DeactivateAsync(fare.Id);

        var reservation = _context.SeatReservations.Single(r => r.SeatId == _seed.Seat1.Id);
        reservation.QuotedFare.Should().Be(15000m);
        reservation.PaidAllocation.Should().Be(15000m);
        _context.Bookings.Single(b => b.Id == booking.Id).QuotedTotal.Should().Be(15000m);
    }

    [Fact]
    public async Task Update_InactiveRuleToTheStartOfAnotherRule_ShouldConflict()
    {
        var classId = NewClass().Id;
        var first = await Service().CreateAsync(Create(classId, fromDays: 10, toDays: 12));
        var second = await Service().CreateAsync(Create(classId, fromDays: 20, toDays: 22));
        await Service().DeactivateAsync(second);

        var act = () => Service().UpdateAsync(second, new UpdateFareRuleRequest(12000m, _now.AddDays(10), _now.AddDays(11)));

        await act.Should().ThrowAsync<ConflictException>(); // unique index (TripType, SeatClassId, EffectiveFrom) tính cả bảng đã tắt
        (await Service().GetByIdAsync(second)).EffectiveFrom.Should().Be(_now.AddDays(20));
        (await Service().GetByIdAsync(first)).EffectiveFrom.Should().Be(_now.AddDays(10));
    }

    [Fact]
    public async Task Deactivate_ShouldFreeTheScopeForANewRule()
    {
        var existing = SeedFare();
        await Service().DeactivateAsync(existing.Id);

        await Service().CreateAsync(Create(_seed.StandardClass.Id, fromDays: 2));

        (await Service().GetByIdAsync(existing.Id)).Status.Should().Be("Inactive");
    }

    [Fact]
    public async Task List_ShouldFilterByTripTypeAndActivity()
    {
        var all = await Service().GetPageAsync(new GetFareRulesRequest());
        var sightseeing = await Service().GetPageAsync(new GetFareRulesRequest(TripType: "Sightseeing"));
        await Service().DeactivateAsync(SeedFare().Id);
        var inactive = await Service().GetPageAsync(new GetFareRulesRequest(IsActive: false));

        all.TotalCount.Should().BeGreaterThan(sightseeing.TotalCount);
        sightseeing.Items.Should().OnlyContain(f => f.TripType == "Sightseeing");
        inactive.Items.Should().ContainSingle().Which.Status.Should().Be("Inactive");
    }

    [Fact]
    public async Task Get_Unknown_ShouldBeNotFound()
    {
        var act = () => Service().GetByIdAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public void Validator_ShouldRejectBadTypeCurrencyPriceAndDates()
    {
        var v = new CreateFareRuleRequestValidator();
        var ok = Create(Guid.NewGuid());

        v.Validate(ok).IsValid.Should().BeTrue();
        v.Validate(ok with { TripType = "Ferry" }).IsValid.Should().BeFalse();
        v.Validate(ok with { Currency = "USD" }).IsValid.Should().BeFalse();
        v.Validate(ok with { Price = -1m }).IsValid.Should().BeFalse();
        v.Validate(ok with { Price = 12000.123m }).IsValid.Should().BeFalse();
        v.Validate(ok with { Price = 0m }).IsValid.Should().BeTrue();
        v.Validate(ok with { EffectiveTo = ok.EffectiveFrom }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Controller_ShouldRequireAdmin()
    {
        typeof(AdminFaresController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>()
            .Should().ContainSingle(a => a.Policy == "AdminOnly");
        typeof(AdminFaresController).GetMethods().SelectMany(m => m.GetCustomAttributes(typeof(AllowAnonymousAttribute), true)).Should().BeEmpty();
    }
}
