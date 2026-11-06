using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.DTOs.Requests.SeatInventory;
using WaterbusSystem.Application.Features.SeatInventory.Services;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.Infrastructure.Repositories;
using WaterbusSystem.Infrastructure.Services.Checkout;
using WaterbusSystem.UnitTests.Bookings;
using WaterbusSystem.UnitTests.Domain;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;
using ValidationException = WaterbusSystem.Application.Common.Exceptions.ValidationException;

namespace WaterbusSystem.UnitTests.Boats;

/// <summary>
/// Bằng chứng trên SQL Server thật (nhiều DbContext/kết nối độc lập) cho thay đổi Seat/SeatClass đồng thời với đặt chỗ.
/// Cả hai phía cùng chạy trong khoá thương mại (SqlCommercialTransactionRunner) nên kết quả hợp lệ chỉ có hai thứ tự:
/// đặt chỗ thắng ⇒ thay đổi bị từ chối; thay đổi thắng ⇒ đặt chỗ thấy trạng thái mới. Không bao giờ có đặt chỗ trên ghế/hạng đã ngừng
/// hoặc snapshot hạng ghế lệch với hạng hiện tại. Cần WATERBUS_TEST_SQLSERVER (mỗi vòng dùng DB tạm riêng, tự xoá).
/// </summary>
[Collection(SqlCollections.Serial)]
public sealed class SeatInventorySqlTests : IAsyncLifetime
{
    private const string Prefix = "WaterbusDomainTests_SeatInv_";
    private const int Rounds = 3;
    private readonly List<string> _databases = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (var connection in _databases)
        {
            if (!new SqlConnectionStringBuilder(connection).InitialCatalog.StartsWith(Prefix)) throw new InvalidOperationException("Unsafe test database.");
            await using var c = Context(connection);
            await c.Database.EnsureDeletedAsync();
        }
    }

    private static ApplicationDbContext Context(string connection) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connection).Options);

    private sealed record Round(string Connection, TestDbContextFactory.TripSeedData Seed)
    {
        public ApplicationDbContext Context() => SeatInventorySqlTests.Context(Connection);
    }

    private async Task<Round> NewRoundAsync()
    {
        var configured = Environment.GetEnvironmentVariable("WATERBUS_TEST_SQLSERVER")!;
        var connection = new SqlConnectionStringBuilder(configured) { InitialCatalog = Prefix + Guid.NewGuid().ToString("N") }.ConnectionString;
        _databases.Add(connection);
        await using var migrate = Context(connection);
        await migrate.Database.MigrateAsync();
        await using var seed = Context(connection);
        return new Round(connection, TestDbContextFactory.SeedTrip(seed));
    }

    private static BoatSeatService Seats(ApplicationDbContext c) =>
        new(new BoatSeatRepository(c), new EfUnitOfWork(c), new SqlCommercialTransactionRunner(c));

    private static SeatClassService Classes(ApplicationDbContext c) =>
        new(new SeatClassRepository(c), new EfUnitOfWork(c), new SqlCommercialTransactionRunner(c));

    private static async Task BookAsync(Round r)
    {
        await using var c = r.Context();
        await CommercialCheckoutTests.Builder(c, new SqlCommercialTransactionRunner(c)).CreateAsync(CommercialCheckoutTests.Command(r.Seed), default);
    }

    private static bool IsCleanRejection(Exception e) =>
        // NotFoundException: ghế/hạng đã ngừng bị loại khỏi truy vấn đặt chỗ nên booking báo không tìm thấy (404) — vẫn là từ chối sạch.
        e is ConflictException or ConcurrencyException or SeatAlreadyBookedException or ValidationException or NotFoundException;

    /// <summary>Chạy hai thao tác cùng lúc (rào chắn), trả về lỗi của từng bên (null = thành công).</summary>
    private static async Task<(Exception? First, Exception? Second)> RaceAsync(Func<Task> first, Func<Task> second)
    {
        var gate = new TaskCompletionSource();
        async Task<Exception?> Run(Func<Task> work)
        {
            await gate.Task;
            try { await work(); return null; } catch (Exception e) { return e; }
        }
        var a = Task.Run(() => Run(first));
        var b = Task.Run(() => Run(second));
        gate.SetResult();
        return (await a, await b);
    }

    // ---- Thứ tự tuần tự trên SQL thật (hai chiều của bất biến) ----

    [SqlServerDomainFact]
    public async Task SeatWithOpenReservation_CannotBeDeactivated_OrReclassed()
    {
        var r = await NewRoundAsync();
        await BookAsync(r);

        await using var c = r.Context();
        var seat = r.Seed.Seat1;
        await Assert.ThrowsAsync<ConflictException>(() => Seats(c).DeactivateSeatAsync(r.Seed.Boat.Id, seat.Id));
        await Assert.ThrowsAsync<ConflictException>(() => Seats(c).UpdateSeatAsync(r.Seed.Boat.Id, seat.Id,
            new UpdateBoatSeatRequest(seat.SeatCode, r.Seed.PremiumClass.Id, seat.RowNumber, seat.ColumnNumber)));
        await Assert.ThrowsAsync<ConflictException>(() => Classes(c).DeactivateAsync(r.Seed.StandardClass.Id));

        await using var check = r.Context();
        (await check.Seats.SingleAsync(s => s.Id == seat.Id)).IsActive.Should().BeTrue();
        (await check.SeatClasses.SingleAsync(x => x.Id == r.Seed.StandardClass.Id)).IsActive.Should().BeTrue();
    }

    [SqlServerDomainFact]
    public async Task DeactivatedSeatOrClass_IsRejectedByBooking_AndNothingIsReserved()
    {
        var r = await NewRoundAsync();
        await using (var c = r.Context()) await Seats(c).DeactivateSeatAsync(r.Seed.Boat.Id, r.Seed.Seat1.Id);
        var seatRejection = await Record.ExceptionAsync(() => BookAsync(r));
        seatRejection.Should().NotBeNull().And.Match<Exception>(e => IsCleanRejection(e));

        await using (var c = r.Context()) await Seats(c).ActivateSeatAsync(r.Seed.Boat.Id, r.Seed.Seat1.Id);
        await using (var c = r.Context()) await Classes(c).DeactivateAsync(r.Seed.StandardClass.Id);
        var classRejection = await Record.ExceptionAsync(() => BookAsync(r));
        classRejection.Should().NotBeNull().And.Match<Exception>(e => IsCleanRejection(e));

        await using var check = r.Context();
        (await check.SeatReservations.CountAsync()).Should().Be(0);
    }

    // ---- Đua song song thật giữa đặt chỗ và thay đổi Seat/SeatClass ----

    [SqlServerDomainFact]
    public async Task ConcurrentBookingAndSeatDeactivation_NeverReservesAnInactiveSeat()
    {
        for (var i = 0; i < Rounds; i++)
        {
            var r = await NewRoundAsync();
            var (booking, change) = await RaceAsync(
                () => BookAsync(r),
                async () => { await using var c = r.Context(); await Seats(c).DeactivateSeatAsync(r.Seed.Boat.Id, r.Seed.Seat1.Id); });

            (booking is null && change is null).Should().BeFalse("đặt chỗ và ngừng ghế không thể cùng thành công");
            foreach (var e in new[] { booking, change }.Where(e => e is not null)) IsCleanRejection(e!).Should().BeTrue($"lỗi phải là từ chối nghiệp vụ, gặp {e}");

            await using var check = r.Context();
            var reservations = await check.SeatReservations.CountAsync();
            var seatActive = (await check.Seats.SingleAsync(s => s.Id == r.Seed.Seat1.Id)).IsActive;
            if (reservations == 1) seatActive.Should().BeTrue("đặt chỗ thắng thì ghế phải còn hoạt động");
            else { reservations.Should().Be(0); seatActive.Should().BeFalse("ngừng ghế thắng thì không có đặt chỗ"); }
        }
    }

    [SqlServerDomainFact]
    public async Task ConcurrentBookingAndSeatClassChange_NeverLeavesStaleClassSnapshot()
    {
        for (var i = 0; i < Rounds; i++)
        {
            var r = await NewRoundAsync();
            var seat = r.Seed.Seat1;
            var (booking, change) = await RaceAsync(
                () => BookAsync(r),
                async () =>
                {
                    await using var c = r.Context();
                    await Seats(c).UpdateSeatAsync(r.Seed.Boat.Id, seat.Id,
                        new UpdateBoatSeatRequest(seat.SeatCode, r.Seed.PremiumClass.Id, seat.RowNumber, seat.ColumnNumber));
                });

            foreach (var e in new[] { booking, change }.Where(e => e is not null)) IsCleanRejection(e!).Should().BeTrue($"lỗi phải là từ chối nghiệp vụ, gặp {e}");

            await using var check = r.Context();
            var finalClass = (await check.Seats.Include(s => s.SeatClass).SingleAsync(s => s.Id == seat.Id)).SeatClass!;
            var reservation = await check.SeatReservations.SingleOrDefaultAsync();
            if (reservation is not null)
                reservation.SeatClassAtSale.Should().Be(finalClass.Name, "snapshot hạng ghế phải khớp hạng ghế cuối cùng của ghế");
            if (booking is null && change is null)
                finalClass.Id.Should().Be(r.Seed.PremiumClass.Id, "cùng thành công chỉ hợp lệ khi đổi hạng chạy trước đặt chỗ");
            if (change is not null) finalClass.Id.Should().Be(r.Seed.StandardClass.Id);
        }
    }

    [SqlServerDomainFact]
    public async Task ConcurrentBookingAndSeatClassDeactivation_NeverReservesInactiveClass()
    {
        for (var i = 0; i < Rounds; i++)
        {
            var r = await NewRoundAsync();
            var (booking, change) = await RaceAsync(
                () => BookAsync(r),
                async () => { await using var c = r.Context(); await Classes(c).DeactivateAsync(r.Seed.StandardClass.Id); });

            (booking is null && change is null).Should().BeFalse("đặt chỗ và ngừng hạng ghế không thể cùng thành công");
            foreach (var e in new[] { booking, change }.Where(e => e is not null)) IsCleanRejection(e!).Should().BeTrue($"lỗi phải là từ chối nghiệp vụ, gặp {e}");

            await using var check = r.Context();
            var reservations = await check.SeatReservations.CountAsync();
            var classActive = (await check.SeatClasses.SingleAsync(x => x.Id == r.Seed.StandardClass.Id)).IsActive;
            if (reservations == 1) classActive.Should().BeTrue("đặt chỗ thắng thì hạng ghế phải còn hoạt động");
            else { reservations.Should().Be(0); classActive.Should().BeFalse(); }
        }
    }
}
