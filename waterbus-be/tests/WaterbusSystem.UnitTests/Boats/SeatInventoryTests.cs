using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using WaterbusSystem.Application.DTOs.Requests.Boats;
using WaterbusSystem.Application.DTOs.Requests.SeatInventory;
using WaterbusSystem.Application.Features.Boats.Services;
using WaterbusSystem.Application.Features.SeatInventory.Services;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.Infrastructure.Repositories;
using WaterbusSystem.UnitTests.TestHelpers;
using WaterbusSystem.WebApi.Controllers;
using Xunit;
using ValidationException = WaterbusSystem.Application.Common.Exceptions.ValidationException;

namespace WaterbusSystem.UnitTests.Boats;

public class SeatInventoryTests
{
    private readonly ApplicationDbContext _context = TestDbContextFactory.CreateDbContext();
    private readonly TestDbContextFactory.TripSeedData _seed;

    public SeatInventoryTests() => _seed = TestDbContextFactory.SeedTrip(_context);

    private BoatSeatService Seats() =>
        new(new BoatSeatRepository(_context), new EfUnitOfWork(_context), new DirectCommercialTransactionRunner());

    private SeatClassService Classes() =>
        new(new SeatClassRepository(_context), new EfUnitOfWork(_context), new DirectCommercialTransactionRunner());

    private BoatService Boats() => new(new BoatRepository(_context), new EfUnitOfWork(_context));

    /// <summary>Tàu mới chưa có ghế, khai báo sức chứa <paramref name="total"/>.</summary>
    private Boat NewBoat(int total)
    {
        var boat = new Boat
        {
            Code = $"NB{Guid.NewGuid():N}"[..10], Name = "New", RegistrationNumber = Guid.NewGuid().ToString("N")[..12],
            TotalSeats = total, Manufacturer = "T", YearBuilt = 2024, IsActive = true
        };
        _context.Boats.Add(boat);
        _context.SaveChanges();
        return boat;
    }

    private static SeatInput Seat(string code, Guid classId, int row = 1, int col = 1) => new(code, classId, row, col);

    private void ReserveSeat(Seat seat, ReservationStatus status = ReservationStatus.Pending, TripStatus tripStatus = TripStatus.Scheduled)
    {
        _seed.Trip.Status = tripStatus;
        var booking = TestDbContextFactory.SeedBooking(_context, _seed, from: 1, to: 2);
        _context.SeatReservations.Add(new SeatReservation
        {
            Booking = booking, BookingId = booking.Id, TripId = _seed.Trip.Id, SeatId = seat.Id, Status = status,
            BoardingStopOrder = 1, DisembarkingStopOrder = 2, SeatCodeAtSale = seat.SeatCode, SeatClassAtSale = "Tieu chuan"
        });
        _context.SaveChanges();
    }

    // ---- Tạo tàu rồi cấu hình ghế ----

    [Fact]
    public async Task NewBoat_StartsNotConfigured_ThenSeatsMakeItComplete()
    {
        var boat = NewBoat(2);
        (await Boats().GetByIdAsync(boat.Id)).SeatInventoryState.Should().Be("NotConfigured");

        var result = await Seats().AddSeatsAsync(boat.Id, new AddBoatSeatsRequest(
            [Seat("A1", _seed.StandardClass.Id, 1, 1), Seat("A2", _seed.PremiumClass.Id, 1, 2)]));

        result.ActiveSeats.Should().Be(2);
        result.State.Should().Be("Complete");
        (await Boats().GetByIdAsync(boat.Id)).ConfiguredSeats.Should().Be(2);
    }

    [Fact]
    public async Task PartialInventory_ShouldBeExposedAsPartial()
    {
        var boat = NewBoat(5);
        await Seats().AddSeatsAsync(boat.Id, new AddBoatSeatsRequest([Seat("A1", _seed.StandardClass.Id)]));

        (await Boats().GetByIdAsync(boat.Id)).SeatInventoryState.Should().Be("Partial");
    }

    [Fact]
    public async Task AddSeats_DuplicateCodeOnBoatOrInRequest_ShouldBeRejectedAtomically()
    {
        var boat = NewBoat(5);
        await Seats().AddSeatsAsync(boat.Id, new AddBoatSeatsRequest([Seat("A1", _seed.StandardClass.Id, 1, 1)]));

        var existing = () => Seats().AddSeatsAsync(boat.Id, new AddBoatSeatsRequest(
            [Seat("B1", _seed.StandardClass.Id, 2, 1), Seat("a1", _seed.StandardClass.Id, 2, 2)]));
        var inRequest = () => Seats().AddSeatsAsync(boat.Id, new AddBoatSeatsRequest(
            [Seat("C1", _seed.StandardClass.Id, 3, 1), Seat("C1", _seed.StandardClass.Id, 3, 2)]));

        await existing.Should().ThrowAsync<ValidationException>();
        await inRequest.Should().ThrowAsync<ValidationException>();
        (await Seats().GetInventoryAsync(boat.Id)).Seats.Select(s => s.SeatCode).Should().Equal("A1"); // không ghế nào lọt vào
    }

    [Fact]
    public async Task AddSeats_UnknownOrInactiveSeatClass_ShouldBeRejected()
    {
        var boat = NewBoat(5);
        _seed.PremiumClass.IsActive = false;
        _context.SaveChanges();

        var inactive = () => Seats().AddSeatsAsync(boat.Id, new AddBoatSeatsRequest([Seat("A1", _seed.PremiumClass.Id)]));
        var unknown = () => Seats().AddSeatsAsync(boat.Id, new AddBoatSeatsRequest([Seat("A1", Guid.NewGuid())]));

        await inactive.Should().ThrowAsync<ValidationException>();
        await unknown.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task AddSeats_SamePositionAsActiveSeat_ShouldBeRejected()
    {
        var boat = NewBoat(5);
        await Seats().AddSeatsAsync(boat.Id, new AddBoatSeatsRequest([Seat("A1", _seed.StandardClass.Id, 1, 1)]));

        var act = () => Seats().AddSeatsAsync(boat.Id, new AddBoatSeatsRequest([Seat("A2", _seed.StandardClass.Id, 1, 1)]));

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task AddSeats_BeyondTotalSeats_ShouldConflict()
    {
        var boat = NewBoat(1);

        var act = () => Seats().AddSeatsAsync(boat.Id, new AddBoatSeatsRequest(
            [Seat("A1", _seed.StandardClass.Id, 1, 1), Seat("A2", _seed.StandardClass.Id, 1, 2)]));

        await act.Should().ThrowAsync<ConflictException>();
        (await Seats().GetInventoryAsync(boat.Id)).ActiveSeats.Should().Be(0);
    }

    [Fact]
    public async Task AddSeats_UnknownBoat_ShouldBeNotFound()
    {
        var act = () => Seats().AddSeatsAsync(Guid.NewGuid(), new AddBoatSeatsRequest([Seat("A1", _seed.StandardClass.Id)]));

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ---- Đổi TotalSeats an toàn ----

    [Fact]
    public async Task UpdateBoat_TotalSeatsBelowActiveSeats_ShouldConflict_AndNotTouchSeats()
    {
        var act = () => Boats().UpdateAsync(_seed.Boat.Id, new UpdateBoatRequest(
            "Test Boat", "REG-TEST", 1, "Test", 2024, true, null));

        await act.Should().ThrowAsync<ConflictException>();
        (await Seats().GetInventoryAsync(_seed.Boat.Id)).ActiveSeats.Should().Be(2);
    }

    [Fact]
    public async Task UpdateBoat_RaisingTotalSeats_ShouldNotGenerateSeats()
    {
        await Boats().UpdateAsync(_seed.Boat.Id, new UpdateBoatRequest("Test Boat", "REG-TEST", 10, "Test", 2024, true, null));

        var inv = await Seats().GetInventoryAsync(_seed.Boat.Id);
        inv.ActiveSeats.Should().Be(2);
        inv.State.Should().Be("Partial");
    }

    // ---- Ghế đang được đặt/giữ ----

    [Fact]
    public async Task DeactivateSeat_WithPendingReservationOnOpenTrip_ShouldConflict()
    {
        ReserveSeat(_seed.Seat1);

        var act = () => Seats().DeactivateSeatAsync(_seed.Boat.Id, _seed.Seat1.Id);

        await act.Should().ThrowAsync<ConflictException>();
        _context.Seats.Single(s => s.Id == _seed.Seat1.Id).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task DeactivateSeat_WithConfirmedReservationOnOpenTrip_ShouldConflict()
    {
        ReserveSeat(_seed.Seat1, ReservationStatus.Confirmed, TripStatus.Boarding);

        var act = () => Seats().DeactivateSeatAsync(_seed.Boat.Id, _seed.Seat1.Id);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task DeactivateSeat_OnlyHistoricalOrCancelledReservations_ShouldSucceed_AndKeepSnapshots()
    {
        ReserveSeat(_seed.Seat1, ReservationStatus.Confirmed, TripStatus.Completed);

        var result = await Seats().DeactivateSeatAsync(_seed.Boat.Id, _seed.Seat1.Id);

        result.ActiveSeats.Should().Be(1);
        var reservation = _context.SeatReservations.Single(r => r.SeatId == _seed.Seat1.Id);
        reservation.SeatCodeAtSale.Should().Be("S01");
        reservation.SeatClassAtSale.Should().Be("Tieu chuan");
    }

    [Fact]
    public async Task UpdateSeat_ChangingClassOrCodeWithCommitment_ShouldConflict_ButPositionChangeIsAllowed()
    {
        ReserveSeat(_seed.Seat1);

        var recode = () => Seats().UpdateSeatAsync(_seed.Boat.Id, _seed.Seat1.Id,
            new UpdateBoatSeatRequest("ZZ", _seed.StandardClass.Id, 1, 1));
        var reclass = () => Seats().UpdateSeatAsync(_seed.Boat.Id, _seed.Seat1.Id,
            new UpdateBoatSeatRequest("S01", _seed.PremiumClass.Id, 1, 1));
        await recode.Should().ThrowAsync<ConflictException>();
        await reclass.Should().ThrowAsync<ConflictException>();

        var moved = await Seats().UpdateSeatAsync(_seed.Boat.Id, _seed.Seat1.Id,
            new UpdateBoatSeatRequest("S01", _seed.StandardClass.Id, 5, 5));
        moved.Seats.Single(s => s.Id == _seed.Seat1.Id).RowNumber.Should().Be(5);
    }

    [Fact]
    public async Task UpdateSeat_ToCodeOfAnotherSeat_ShouldBeRejected()
    {
        var act = () => Seats().UpdateSeatAsync(_seed.Boat.Id, _seed.Seat1.Id,
            new UpdateBoatSeatRequest("S02", _seed.StandardClass.Id, 1, 1));

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task UpdateSeat_SeatOfAnotherBoat_ShouldBeNotFound()
    {
        var act = () => Seats().UpdateSeatAsync(_seed.Boat.Id, _seed.SeatOnOtherBoat.Id,
            new UpdateBoatSeatRequest("X01", _seed.StandardClass.Id, 1, 1));

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ActivateSeat_BeyondCapacity_ShouldConflict()
    {
        await Seats().DeactivateSeatAsync(_seed.Boat.Id, _seed.Seat1.Id);
        await Seats().AddSeatsAsync(_seed.Boat.Id, new AddBoatSeatsRequest([Seat("N1", _seed.StandardClass.Id, 9, 9)]));

        var act = () => Seats().ActivateSeatAsync(_seed.Boat.Id, _seed.Seat1.Id);

        await act.Should().ThrowAsync<ConflictException>();
    }

    // ---- SeatClass ----

    [Fact]
    public async Task SeatClass_CreateUpdateListAndDuplicateCode()
    {
        var id = await Classes().CreateAsync(new CreateSeatClassRequest(" SC99 ", "Boong", "mô tả"));
        await Classes().UpdateAsync(id, new UpdateSeatClassRequest("Boong 2", null));

        var detail = await Classes().GetByIdAsync(id);
        detail.Code.Should().Be("SC99");
        detail.Name.Should().Be("Boong 2");
        (await Classes().ListAsync(new GetSeatClassesRequest())).Should().Contain(c => c.Id == id);

        var dup = () => Classes().CreateAsync(new CreateSeatClassRequest("SC99", "X", null));
        await dup.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task SeatClass_DeactivateWithOpenCommitment_ShouldConflict()
    {
        ReserveSeat(_seed.Seat1);

        var act = () => Classes().DeactivateAsync(_seed.StandardClass.Id);

        await act.Should().ThrowAsync<ConflictException>();
        _context.SeatClasses.Single(c => c.Id == _seed.StandardClass.Id).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task SeatClass_DeactivateWithoutCommitments_HidesFromDefaultListButNotFromFullList()
    {
        await Classes().DeactivateAsync(_seed.PremiumClass.Id);

        (await Classes().ListAsync(new GetSeatClassesRequest())).Should().NotContain(c => c.Id == _seed.PremiumClass.Id);
        (await Classes().ListAsync(new GetSeatClassesRequest(true))).Should().Contain(c => c.Id == _seed.PremiumClass.Id && !c.IsActive);

        await Classes().ActivateAsync(_seed.PremiumClass.Id);
        _context.SeatClasses.Single(c => c.Id == _seed.PremiumClass.Id).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task SeatClass_UnknownId_ShouldBeNotFound()
    {
        var act = () => Classes().GetByIdAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task SoftDeletedSeatCode_ShouldStillBeTreatedAsTaken()
    {
        var boat = NewBoat(5);
        await Seats().AddSeatsAsync(boat.Id, new AddBoatSeatsRequest(
            [Seat("A1", _seed.StandardClass.Id, 1, 1), Seat("A2", _seed.StandardClass.Id, 1, 2)]));
        _context.Seats.Single(s => s.BoatId == boat.Id && s.SeatCode == "A1").IsDeleted = true;
        _context.SaveChanges();

        var add = () => Seats().AddSeatsAsync(boat.Id, new AddBoatSeatsRequest([Seat("A1", _seed.StandardClass.Id, 2, 1)]));
        var rename = () => Seats().UpdateSeatAsync(boat.Id, _context.Seats.Single(s => s.BoatId == boat.Id && s.SeatCode == "A2").Id,
            new UpdateBoatSeatRequest("A1", _seed.StandardClass.Id, 1, 2));

        await add.Should().ThrowAsync<ValidationException>();
        await rename.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task SoftDeletedSeatClassCode_ShouldStillBeTreatedAsTaken()
    {
        _seed.PremiumClass.IsDeleted = true;
        _context.SaveChanges();

        var act = () => Classes().CreateAsync(new CreateSeatClassRequest(_seed.PremiumClass.Code, "Again", null));

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public void SeatClassName_ShouldFitSnapshotColumns()
    {
        var create = new WaterbusSystem.Application.Validators.SeatInventory.CreateSeatClassRequestValidator();
        var update = new WaterbusSystem.Application.Validators.SeatInventory.UpdateSeatClassRequestValidator();

        create.Validate(new CreateSeatClassRequest("C1", new string('x', 50), null)).IsValid.Should().BeTrue();
        create.Validate(new CreateSeatClassRequest("C1", new string('x', 51), null)).IsValid.Should().BeFalse();
        update.Validate(new UpdateSeatClassRequest(new string('x', 51), null)).IsValid.Should().BeFalse();
    }

    // ---- Phân quyền ----

    [Theory]
    [InlineData(typeof(SeatClassesController))]
    [InlineData(typeof(BoatSeatsController))]
    public void SeatInventoryControllers_ShouldRequireAdmin(Type controller)
    {
        var authorize = controller.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().ToList();

        authorize.Should().ContainSingle(a => a.Policy == "AdminOnly");
        controller.GetMethods().SelectMany(m => m.GetCustomAttributes(typeof(AllowAnonymousAttribute), true)).Should().BeEmpty();
    }

    // ---- Validators ----

    [Fact]
    public void AddSeatsValidator_RejectsEmptyAndOversizedBatchesAndBadSeats()
    {
        var v = new WaterbusSystem.Application.Validators.SeatInventory.AddBoatSeatsRequestValidator();

        v.Validate(new AddBoatSeatsRequest([])).IsValid.Should().BeFalse();
        v.Validate(new AddBoatSeatsRequest(Enumerable.Range(1, 201).Select(i => Seat($"S{i}", Guid.NewGuid(), 1, 1)).ToList())).IsValid.Should().BeFalse();
        v.Validate(new AddBoatSeatsRequest([Seat("", Guid.NewGuid())])).IsValid.Should().BeFalse();
        v.Validate(new AddBoatSeatsRequest([Seat("TOOLONGCODE1", Guid.NewGuid())])).IsValid.Should().BeFalse();
        v.Validate(new AddBoatSeatsRequest([Seat("A1", Guid.NewGuid(), 0, 1)])).IsValid.Should().BeFalse();
        v.Validate(new AddBoatSeatsRequest([Seat("A1", Guid.NewGuid(), 1, 1)])).IsValid.Should().BeTrue();
    }
}
