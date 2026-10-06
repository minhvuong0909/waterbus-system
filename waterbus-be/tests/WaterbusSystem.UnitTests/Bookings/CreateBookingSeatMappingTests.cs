using FluentAssertions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Bookings.Commands.CreateBooking;
using WaterbusSystem.Application.Features.Bookings.Services;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Infrastructure.Services;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;

namespace WaterbusSystem.UnitTests.Bookings;

/// <summary>
/// Các test này bảo vệ hai điều kiện dễ hỏng nguy hiểm trong luồng giữ chỗ:
///   1. Tên hành khách phải gắn đúng với ghế khách CHỌN, không phụ thuộc thứ tự CSDL trả về.
///   2. Redis/CheckoutSession hỏng KHÔNG được làm hỏng giao dịch đã ghi xuống SQL Server.
/// </summary>
public class CreateBookingSeatMappingTests
{
    private static CreateBookingCommand CommandFor(
        TestDbContextFactory.TripSeedData seed,
        List<Guid> seatIds,
        IReadOnlyList<PassengerInput>? passengers) =>
        new(
            TripId: seed.Trip.Id,
            SeatIds: seatIds,
            BoardingStationId: seed.StationA.Id,
            DisembarkingStationId: seed.StationC.Id,
            CustomerName: "Nguoi mua",
            CustomerEmail: "buyer@test.com",
            CustomerPhone: "0901234567",
            Passengers: passengers);

    /// <summary>
    /// BR-COM-05/06: khách có thể mua cho nhiều người, mỗi ghế tương ứng đúng một hành khách.
    /// Client gửi Passengers theo đúng thứ tự SeatIds, nên hệ thống phải tôn trọng thứ tự đó.
    /// </summary>
    [Fact]
    public async Task Handle_WhenPassengersProvided_ShouldMapEachPassengerToTheSeatAtTheSameIndex()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var seed = TestDbContextFactory.SeedTrip(context);
        var handler = new CreateBookingCommandHandler(
            context, new FakeDistributedLockService(), new SeatAvailabilityService(context));

        // Cố tình đảo thứ tự ghế so với thứ tự seed trong CSDL.
        var requestedSeatIds = new List<Guid> { seed.Seat2.Id, seed.Seat1.Id };
        var passengers = new List<PassengerInput>
        {
            new("Nguyen Van A", "a@test.com", null),
            new("Tran Thi B", null, null)
        };

        var result = await handler.Handle(
            CommandFor(seed, requestedSeatIds, passengers), CancellationToken.None);

        var reservations = context.SeatReservations.Where(r => r.BookingId == result.BookingId).ToList();
        reservations.Should().HaveCount(2);

        // S02 phải mang tên "Nguyen Van A" vì khách xếp A vào ghế thứ nhất trong danh sách.
        var seat2Reservation = reservations.Single(r => r.SeatId == seed.Seat2.Id);
        seat2Reservation.PassengerName.Should().Be("Nguyen Van A");
        seat2Reservation.SeatCodeAtSale.Should().Be(seed.Seat2.SeatCode);

        var seat1Reservation = reservations.Single(r => r.SeatId == seed.Seat1.Id);
        seat1Reservation.PassengerName.Should().Be("Tran Thi B");
        seat1Reservation.SeatCodeAtSale.Should().Be(seed.Seat1.SeatCode);
    }

    [Fact]
    public async Task Handle_WhenPassengersNotProvided_ShouldPutPurchaserOnEverySeat()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var seed = TestDbContextFactory.SeedTrip(context);
        var handler = new CreateBookingCommandHandler(
            context, new FakeDistributedLockService(), new SeatAvailabilityService(context));

        var result = await handler.Handle(
            CommandFor(seed, [seed.Seat1.Id, seed.Seat2.Id], null), CancellationToken.None);

        context.SeatReservations
            .Where(r => r.BookingId == result.BookingId)
            .Should().OnlyContain(r => r.PassengerName == "Nguoi mua");
    }

    [Fact]
    public async Task Handle_WhenPassengerCountDoesNotMatchSeatCount_ShouldReject()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var seed = TestDbContextFactory.SeedTrip(context);
        var handler = new CreateBookingCommandHandler(
            context, new FakeDistributedLockService(), new SeatAvailabilityService(context));

        var command = CommandFor(
            seed,
            [seed.Seat1.Id, seed.Seat2.Id],
            [new PassengerInput("Chi mot nguoi", null, null)]);

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<Application.Common.Exceptions.ValidationException>();
    }

    /// <summary>
    /// BR-HOLD-04: Redis chỉ hỗ trợ trải nghiệm. Nếu Redis lỗi mà vẫn ném exception ra ngoài,
    /// khách thấy lỗi rồi bấm lại sẽ tạo thêm một Order trùng trong khi Order cũ đã tồn tại.
    /// </summary>
    [Fact]
    public async Task Handle_WhenCheckoutSessionStoreFails_ShouldStillReturnSuccessfullyCreatedBooking()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var seed = TestDbContextFactory.SeedTrip(context);

        var failingSession = new CheckoutSessionService(
            new ThrowingCheckoutSessionStore(),
            new FixedClock { Now = DateTimeOffset.UtcNow },
            TimeSpan.FromMinutes(10),
            TimeSpan.FromMinutes(15));

        var handler = new CreateBookingCommandHandler(
            context, new FakeDistributedLockService(), new SeatAvailabilityService(context), failingSession);

        var result = await handler.Handle(
            CommandFor(seed, [seed.Seat1.Id], null), CancellationToken.None);

        // Vẫn trả kết quả đặt vé thành công dù Redis chết.
        result.BookingId.Should().NotBe(Guid.Empty);
        context.Bookings.Should().ContainSingle(b => b.Id == result.BookingId);
        context.SeatReservations.Should().ContainSingle(r => r.BookingId == result.BookingId);

        // Phiên Redis không tồn tại nên client không có mã phiên để đếm ngược,
        // nhưng cửa sổ giữ chỗ vẫn phải được báo bằng TTL mặc định.
        result.CheckoutSessionId.Should().NotBeNull();
        result.HoldDurationSeconds.Should().BeGreaterThan(0);
    }

    private sealed class ThrowingCheckoutSessionStore : ICheckoutSessionStore
    {
        public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task SetAsync(string key, string value, TimeSpan ttl, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Redis không sẵn sàng.");

        public Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class FixedClock : IDateTimeService
    {
        public DateTimeOffset Now { get; set; }
    }
}