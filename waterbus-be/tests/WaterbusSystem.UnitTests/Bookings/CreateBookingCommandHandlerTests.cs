using FluentAssertions;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Features.Bookings.Commands.CreateBooking;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure.Services;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;
using ValidationException = WaterbusSystem.Application.Common.Exceptions.ValidationException;

namespace WaterbusSystem.UnitTests.Bookings;

/// <summary>
/// Test thật cho CreateBookingCommandHandler (chạy trên EF Core InMemory DbContext thật,
/// không mock IApplicationDbContext) - bao phủ: tính giá theo PriceMultiplier, chặn ghế sai tàu,
/// tái sử dụng ghế trên chặng không giao thoa, chặn đặt trùng chặng, và race condition concurrency.
/// </summary>
public class CreateBookingCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenValidRequest_ShouldCreateBookingWithPriceMultiplierApplied()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var seed = TestDbContextFactory.SeedTrip(context);

        var handler = new CreateBookingCommandHandler(context, new FakeDistributedLockService(), new SeatAvailabilityService(context));

        var command = new CreateBookingCommand(
            TripId: seed.Trip.Id,
            SeatIds: new List<Guid> { seed.Seat1.Id, seed.Seat2.Id },
            BoardingStationId: seed.StationA.Id,
            DisembarkingStationId: seed.StationC.Id,
            CustomerName: "Nguyen Van A",
            CustomerEmail: "a@test.com",
            CustomerPhone: "0901234567");

        var result = await handler.Handle(command, CancellationToken.None);

        // BasePrice 15000, Seat1 x1.0 = 15000, Seat2 x1.2 = 18000 -> Tổng 33000 (trước đây bug bỏ qua PriceMultiplier)
        result.TotalAmount.Should().Be(33000m);

        var booking = context.Bookings.Single(b => b.Id == result.BookingId);
        context.Entry(booking).Collection(b => b.Tickets).Load();
        booking.Tickets.Should().HaveCount(2);

        var reservations = context.SeatReservations.Where(r => r.BookingId == booking.Id).ToList();
        reservations.Should().HaveCount(2);
        reservations.Should().OnlyContain(r => r.BoardingStopOrder == 1 && r.DisembarkingStopOrder == 3);
    }

    [Fact]
    public async Task Handle_WhenSeatBelongsToDifferentBoat_ShouldThrowValidationException()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var seed = TestDbContextFactory.SeedTrip(context);

        var handler = new CreateBookingCommandHandler(context, new FakeDistributedLockService(), new SeatAvailabilityService(context));

        // seed.SeatOnOtherBoat thuộc otherBoat, không phải boat vận hành seed.Trip
        var command = new CreateBookingCommand(
            TripId: seed.Trip.Id,
            SeatIds: new List<Guid> { seed.SeatOnOtherBoat.Id },
            BoardingStationId: seed.StationA.Id,
            DisembarkingStationId: seed.StationC.Id,
            CustomerName: "Nguyen Van A",
            CustomerEmail: "a@test.com",
            CustomerPhone: "0901234567");

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_WhenSegmentsDoNotOverlap_ShouldAllowSameSeatToBeBookedTwice()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var seed = TestDbContextFactory.SeedTrip(context);

        var handler = new CreateBookingCommandHandler(context, new FakeDistributedLockService(), new SeatAvailabilityService(context));

        // Khách 1: A -> B (chặng [1,2))
        var firstBooking = await handler.Handle(new CreateBookingCommand(
            seed.Trip.Id, new List<Guid> { seed.Seat1.Id }, seed.StationA.Id, seed.StationB.Id,
            "Khach 1", "k1@test.com", "0901111111"), CancellationToken.None);

        // Khách 2: B -> C (chặng [2,3)) - cùng ghế S01 nhưng KHÔNG giao thoa với khách 1 -> phải được phép
        var secondBooking = await handler.Handle(new CreateBookingCommand(
            seed.Trip.Id, new List<Guid> { seed.Seat1.Id }, seed.StationB.Id, seed.StationC.Id,
            "Khach 2", "k2@test.com", "0902222222"), CancellationToken.None);

        firstBooking.BookingId.Should().NotBe(secondBooking.BookingId);

        var reservations = context.SeatReservations.Where(r => r.SeatId == seed.Seat1.Id).ToList();
        reservations.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_WhenSegmentsOverlap_ShouldThrowConcurrencyException()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var seed = TestDbContextFactory.SeedTrip(context);

        var handler = new CreateBookingCommandHandler(context, new FakeDistributedLockService(), new SeatAvailabilityService(context));

        // Khách 1: A -> C (toàn tuyến, chặng [1,3))
        await handler.Handle(new CreateBookingCommand(
            seed.Trip.Id, new List<Guid> { seed.Seat1.Id }, seed.StationA.Id, seed.StationC.Id,
            "Khach 1", "k1@test.com", "0901111111"), CancellationToken.None);

        // Khách 2: A -> B (chặng [1,2)) - giao thoa với khách 1 -> phải bị từ chối
        var act = () => handler.Handle(new CreateBookingCommand(
            seed.Trip.Id, new List<Guid> { seed.Seat1.Id }, seed.StationA.Id, seed.StationB.Id,
            "Khach 2", "k2@test.com", "0902222222"), CancellationToken.None);

        await act.Should().ThrowAsync<ConcurrencyException>();
    }

    [Fact]
    public async Task Handle_WhenTwoRequestsRaceForSameSeatSameSegment_OnlyOneShouldSucceed()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var seed = TestDbContextFactory.SeedTrip(context);

        // Dùng chung 1 FakeDistributedLockService để mô phỏng 2 request tranh chấp cùng 1 ghế
        var sharedLockService = new FakeDistributedLockService();

        var handler1 = new CreateBookingCommandHandler(context, sharedLockService, new SeatAvailabilityService(context));

        var command = new CreateBookingCommand(
            seed.Trip.Id, new List<Guid> { seed.Seat1.Id }, seed.StationA.Id, seed.StationC.Id,
            "Khach Dua", "race@test.com", "0909999999");

        // Giả lập request 2 đến NGAY khi request 1 đang giữ lock (chưa release) bằng cách chiếm trước lock thủ công
        var manualLock = await sharedLockService.AcquireLockAsync(
            $"lock:trip:{seed.Trip.Id}:seat:{seed.Seat1.Id}", TimeSpan.FromSeconds(30), TimeSpan.Zero, TimeSpan.FromMilliseconds(10));
        manualLock.Should().NotBeNull("lock phải lấy được lần đầu");

        var act = () => handler1.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<SeatAlreadyBookedException>();

        // Sau khi release, request thứ 2 (thực chất là lần thử lại) phải thành công
        await manualLock!.DisposeAsync();

        var result = await handler1.Handle(command, CancellationToken.None);
        result.Should().NotBeNull();
    }
}
