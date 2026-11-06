using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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
/// đặt chỗ thắng ⇒ thay đổi bị từ chối; thay đổi thắng ⇒ đặt chỗ thấy trạng thái mới. Các test đua KHÔNG dựa vào may rủi lịch: một bên bị
/// tạm dừng đúng trước lệnh ghi (sau khi đã kiểm tra) bằng SaveChangesInterceptor, bên kia chạy xen vào — bỏ khoá thương mại là fail.
/// Cần WATERBUS_TEST_SQLSERVER (mỗi test dùng DB tạm riêng, tự xoá).
/// </summary>
[Collection(SqlCollections.Serial)]
public sealed class SeatInventorySqlTests : IAsyncLifetime
{
    private const string Prefix = "WaterbusDomainTests_SeatInv_";
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

    private static async Task BookAsync(Round r, ApplicationDbContext? existing = null)
    {
        await using var own = existing is null ? r.Context() : null;
        var c = existing ?? own!;
        await CommercialCheckoutTests.Builder(c, new SqlCommercialTransactionRunner(c)).CreateAsync(CommercialCheckoutTests.Command(r.Seed), default);
    }

    private static bool IsCleanRejection(Exception e) =>
        // NotFoundException: ghế/hạng đã ngừng bị loại khỏi truy vấn đặt chỗ nên booking báo không tìm thấy (404) — vẫn là từ chối sạch.
        e is ConflictException or ConcurrencyException or SeatAlreadyBookedException or ValidationException or NotFoundException;

    /// <summary>
    /// Tạm dừng một DbContext ngay TRƯỚC lệnh ghi đầu tiên thoả <c>shouldPause</c> (tức là SAU khi nó đã đọc/kiểm tra xong, TRƯỚC khi ghi),
    /// để thao tác đối kháng chạy đúng vào khoảng hở check-then-write. Có khoá thương mại thì thao tác kia bị chặn tại applock
    /// (bên bị dừng chờ tối đa <see cref="HoldMillis"/> ms rồi tự đi tiếp); không có khoá thì thao tác kia commit xen vào giữa.
    /// </summary>
    private sealed class PauseBeforeWriteInterceptor(Func<DbContext, bool> shouldPause, Task<Task> competitor) : SaveChangesInterceptor
    {
        private int _fired;
        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context is { } context && shouldPause(context) && Interlocked.Exchange(ref _fired, 1) == 0)
            {
                Reached.TrySetResult();
                var other = await competitor;
                await Task.WhenAny(other, Task.Delay(HoldMillis, cancellationToken));
            }
            return result;
        }
    }

    private const int HoldMillis = 2500;

    private static bool BookingWrites(DbContext c) => c.ChangeTracker.Entries<SeatReservation>().Any(e => e.State == EntityState.Added);
    private static bool SeatWrites(DbContext c) => c.ChangeTracker.Entries<Seat>().Any(e => e.State == EntityState.Modified);
    private static bool ClassWrites(DbContext c) => c.ChangeTracker.Entries<SeatClass>().Any(e => e.State == EntityState.Modified);

    private static async Task<Exception?> CaptureAsync(Func<Task> work)
    {
        try { await work(); return null; } catch (Exception e) { return e; }
    }

    /// <summary>Chạy <paramref name="pausedOp"/> tới điểm ghi rồi mới cho <paramref name="otherOp"/> chạy xen vào. Trả lỗi (null = thành công) của từng bên.</summary>
    private static async Task<(Exception? Paused, Exception? Other)> PausedRaceAsync(
        Round r, Func<DbContext, bool> pauseWhen, Func<ApplicationDbContext, Task> pausedOp, Func<ApplicationDbContext, Task> otherOp)
    {
        var competitor = new TaskCompletionSource<Task>(TaskCreationOptions.RunContinuationsAsynchronously);
        var interceptor = new PauseBeforeWriteInterceptor(pauseWhen, competitor.Task);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(r.Connection).AddInterceptors(interceptor).Options;

        var pausedTask = Task.Run(() => CaptureAsync(async () => { await using var c = new ApplicationDbContext(options); await pausedOp(c); }));
        var first = await Task.WhenAny(interceptor.Reached.Task, pausedTask);
        if (first == pausedTask)
            throw new InvalidOperationException("Thao tác bị dừng đã kết thúc mà chưa tới điểm ghi: " + pausedTask.Result);

        var otherTask = Task.Run(() => CaptureAsync(async () => { await using var c = r.Context(); await otherOp(c); }));
        competitor.SetResult(otherTask);
        return (await pausedTask, await otherTask);
    }

    private static Task DeactivateSeat(Round r, ApplicationDbContext c) => Seats(c).DeactivateSeatAsync(r.Seed.Boat.Id, r.Seed.Seat1.Id);

    private static Task ReclassSeat(Round r, ApplicationDbContext c) => Seats(c).UpdateSeatAsync(r.Seed.Boat.Id, r.Seed.Seat1.Id,
        new UpdateBoatSeatRequest(r.Seed.Seat1.SeatCode, r.Seed.PremiumClass.Id, r.Seed.Seat1.RowNumber, r.Seed.Seat1.ColumnNumber));

    private static Task DeactivateClass(Round r, ApplicationDbContext c) => Classes(c).DeactivateAsync(r.Seed.StandardClass.Id);

    private static void AssertClean(Exception? e) { if (e is not null) IsCleanRejection(e).Should().BeTrue($"lỗi phải là từ chối nghiệp vụ, gặp {e}"); }

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

    /// <summary>
    /// Tạo SeatClass trùng mã đồng thời (HTTP thật từng cho 1x201 + 1x400 + 4x500 vì check-then-insert nằm ngoài khoá):
    /// phải đúng 1 thành công, các request còn lại bị từ chối nghiệp vụ (400), không lỗi unique-index thô.
    /// </summary>
    [SqlServerDomainFact]
    public async Task ConcurrentDuplicateSeatClassCreate_HasOneWinner_AndOthersAreCleanRejections()
    {
        var r = await NewRoundAsync();
        const int parallelRequests = 6;
        var gate = new TaskCompletionSource();
        var attempts = Enumerable.Range(0, parallelRequests).Select(_ => Task.Run(async () =>
        {
            await gate.Task;
            return await CaptureAsync(async () =>
            {
                await using var c = r.Context();
                await Classes(c).CreateAsync(new CreateSeatClassRequest("DUP-CODE", "Dup", null));
            });
        })).ToList();
        gate.SetResult();
        var outcomes = await Task.WhenAll(attempts);

        outcomes.Count(e => e is null).Should().Be(1, "đúng một request tạo được hạng ghế");
        foreach (var failure in outcomes.Where(e => e is not null))
            failure.Should().BeOfType<ValidationException>($"request thua phải bị từ chối nghiệp vụ, gặp {failure}");
        await using var check = r.Context();
        (await check.SeatClasses.CountAsync(x => x.Code == "DUP-CODE")).Should().Be(1);
    }

    // ---- Đua xác định tại điểm check-then-write (một bên ĐÃ kiểm tra xong, bên kia chen vào trước khi nó ghi) ----

    [SqlServerDomainFact]
    public async Task BookingPausedBeforeWrite_SeatDeactivation_MustBeRejected()
    {
        var r = await NewRoundAsync();
        var (booking, change) = await PausedRaceAsync(r, BookingWrites, c => BookAsync(r, c), c => DeactivateSeat(r, c));

        booking.Should().BeNull("đặt chỗ đã giữ khoá thương mại nên phải hoàn tất");
        change.Should().BeOfType<ConflictException>("ngừng ghế phải thấy đặt chỗ vừa commit, không được chen vào giữa");
        await using var check = r.Context();
        (await check.SeatReservations.CountAsync()).Should().Be(1);
        (await check.Seats.SingleAsync(s => s.Id == r.Seed.Seat1.Id)).IsActive.Should().BeTrue();
    }

    [SqlServerDomainFact]
    public async Task BookingPausedBeforeWrite_SeatClassChange_MustBeRejected()
    {
        var r = await NewRoundAsync();
        var (booking, change) = await PausedRaceAsync(r, BookingWrites, c => BookAsync(r, c), c => ReclassSeat(r, c));

        booking.Should().BeNull();
        change.Should().BeOfType<ConflictException>();
        await using var check = r.Context();
        var seat = await check.Seats.Include(s => s.SeatClass).SingleAsync(s => s.Id == r.Seed.Seat1.Id);
        seat.SeatClassId.Should().Be(r.Seed.StandardClass.Id);
        (await check.SeatReservations.SingleAsync()).SeatClassAtSale.Should().Be(seat.SeatClass!.Name, "snapshot khớp hạng hiện tại của ghế");
    }

    [SqlServerDomainFact]
    public async Task BookingPausedBeforeWrite_SeatClassDeactivation_MustBeRejected()
    {
        var r = await NewRoundAsync();
        var (booking, change) = await PausedRaceAsync(r, BookingWrites, c => BookAsync(r, c), c => DeactivateClass(r, c));

        booking.Should().BeNull();
        change.Should().BeOfType<ConflictException>();
        await using var check = r.Context();
        (await check.SeatReservations.CountAsync()).Should().Be(1);
        (await check.SeatClasses.SingleAsync(x => x.Id == r.Seed.StandardClass.Id)).IsActive.Should().BeTrue();
    }

    [SqlServerDomainFact]
    public async Task SeatDeactivationPausedBeforeWrite_ConcurrentBooking_MustNotReserveInactiveSeat()
    {
        var r = await NewRoundAsync();
        var (change, booking) = await PausedRaceAsync(r, SeatWrites, c => DeactivateSeat(r, c), c => BookAsync(r, c));

        change.Should().BeNull("ngừng ghế đã giữ khoá thương mại nên phải hoàn tất");
        booking.Should().NotBeNull("đặt chỗ phải thấy ghế đã ngừng, không được đặt song song");
        AssertClean(booking);
        await using var check = r.Context();
        (await check.SeatReservations.CountAsync()).Should().Be(0);
        (await check.Seats.SingleAsync(s => s.Id == r.Seed.Seat1.Id)).IsActive.Should().BeFalse();
    }

    [SqlServerDomainFact]
    public async Task SeatClassChangePausedBeforeWrite_ConcurrentBooking_MustSnapshotNewClass()
    {
        var r = await NewRoundAsync();
        var (change, booking) = await PausedRaceAsync(r, SeatWrites, c => ReclassSeat(r, c), c => BookAsync(r, c));

        change.Should().BeNull();
        booking.Should().BeNull("đặt chỗ chờ đổi hạng xong rồi mới chạy trên hạng mới");
        await using var check = r.Context();
        (await check.SeatReservations.SingleAsync()).SeatClassAtSale.Should().Be(r.Seed.PremiumClass.Name, "không được snapshot hạng cũ");
    }

    [SqlServerDomainFact]
    public async Task SeatClassDeactivationPausedBeforeWrite_ConcurrentBooking_MustNotReserveInactiveClass()
    {
        var r = await NewRoundAsync();
        var (change, booking) = await PausedRaceAsync(r, ClassWrites, c => DeactivateClass(r, c), c => BookAsync(r, c));

        change.Should().BeNull();
        booking.Should().NotBeNull("đặt chỗ phải thấy hạng ghế đã ngừng");
        AssertClean(booking);
        await using var check = r.Context();
        (await check.SeatReservations.CountAsync()).Should().Be(0);
        (await check.SeatClasses.SingleAsync(x => x.Id == r.Seed.StandardClass.Id)).IsActive.Should().BeFalse();
    }
}
