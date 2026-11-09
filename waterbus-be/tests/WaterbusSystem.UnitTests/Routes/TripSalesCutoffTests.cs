using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Moq;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Requests.Trips;
using WaterbusSystem.Application.DTOs.Requests.TripPlanning;
using WaterbusSystem.Application.Features.Trips.Services;
using WaterbusSystem.Application.Validators.TripPlanning;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.Infrastructure.Repositories;
using WaterbusSystem.UnitTests.Bookings;
using WaterbusSystem.UnitTests.Domain;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;
using ValidationException = WaterbusSystem.Application.Common.Exceptions.ValidationException;

namespace WaterbusSystem.UnitTests.Routes;

/// <summary>
/// BR-TIME-01/02: SalesCloseAt là mốc đóng bán do Admin cấu hình; mặc định khi không cấu hình chỉ là TRẦN AN TOÀN = giờ khởi hành
/// (không bán sau khi Trip rời bến), không thay thế cấu hình bình thường. Mốc Admin đặt sớm hơn luôn được giữ; không giá trị nào đặt mốc
/// sau giờ khởi hành; rời bến sớm thì đóng bán ngay; Trip chưa có tàu thì không bán được.
/// </summary>
public class TripSalesCutoffTests
{
    private sealed class FixedClock(DateTimeOffset now) : IDateTimeService { public DateTimeOffset Now { get; } = now; }

    private static TripService Trips(ApplicationDbContext c, IDateTimeService clock) =>
        new(new TripRepository(c), new IncidentRepository(c), new EfUnitOfWork(c), Mock.Of<ICurrentUserService>(), clock,
            Mock.Of<IOperationsAlertPublisher>(), Mock.Of<IUserDirectory>(), TestNotifications.Outbox(c), new DirectCommercialTransactionRunner());

    private static GetTripSeatMapRequest SeatMapRequest(TestDbContextFactory.TripSeedData seed) =>
        new(seed.Trip.TripStopCalls.Single(c => c.VisitOrder == 1).Id, seed.Trip.TripStopCalls.Single(c => c.VisitOrder == 3).Id);

    // ---- 4. Rời bến sớm: đóng bán ngay (ghi nhận đóng bán ở TripServiceChangeStatusTests.Start_*; ở đây là chặn đặt chỗ) ----

    [Fact]
    public async Task ActualDepartureBeforeScheduledTime_StopsSalesImmediately_OnBothBookingAndRead()
    {
        using var c = TestDbContextFactory.CreateDbContext();
        var seed = TestDbContextFactory.SeedTrip(c);
        var now = DateTimeOffset.UtcNow;
        seed.Trip.DepartureTime = now.AddMinutes(30);           // lịch còn nửa giờ nữa
        seed.Trip.SalesCloseAt = now.AddMinutes(30);            // mốc đóng bán chưa tới
        seed.Trip.ActualDepartureTime = now.AddMinutes(-1);     // nhưng tàu đã rời bến sớm
        await c.SaveChangesAsync();

        var booking = () => CommercialHandlerFactory.Booking(c).CreateAsync(CommercialCheckoutTests.Command(seed), default);
        await booking.Should().ThrowAsync<ValidationException>();
        (await c.SeatReservations.CountAsync()).Should().Be(0);

        var map = await Trips(c, new FixedClock(now)).GetSeatMapAsync(seed.Trip.Id, SeatMapRequest(seed), default);
        map.CanBook.Should().BeFalse();
        map.SalesState.Should().Be("tripUnavailable");
    }

    // ---- 3. Biên chính xác của mốc đóng bán ----

    [Fact]
    public async Task ReadSide_CutoffExactlyNow_IsClosed_OneMillisecondLaterIsOpen()
    {
        using var c = TestDbContextFactory.CreateDbContext();
        var seed = TestDbContextFactory.SeedTrip(c);
        var now = DateTimeOffset.UtcNow;
        var service = Trips(c, new FixedClock(now));

        seed.Trip.SalesCloseAt = now;
        await c.SaveChangesAsync();
        (await service.GetSeatMapAsync(seed.Trip.Id, SeatMapRequest(seed), default)).SalesState.Should().Be("salesClosed");

        seed.Trip.SalesCloseAt = now.AddMilliseconds(1);
        await c.SaveChangesAsync();
        var open = await service.GetSeatMapAsync(seed.Trip.Id, SeatMapRequest(seed), default);
        open.SalesState.Should().Be("open");
        open.CanBook.Should().BeTrue();
    }

    [Fact]
    public async Task WriteSide_PastCutoffRejected_FutureCutoffAccepted()
    {
        using var c = TestDbContextFactory.CreateDbContext();
        var seed = TestDbContextFactory.SeedTrip(c);

        seed.Trip.SalesCloseAt = DateTimeOffset.UtcNow.AddMilliseconds(-1);
        await c.SaveChangesAsync();
        var closed = () => CommercialHandlerFactory.Booking(c).CreateAsync(CommercialCheckoutTests.Command(seed), default);
        await closed.Should().ThrowAsync<ValidationException>();
        (await c.SeatReservations.CountAsync()).Should().Be(0);

        seed.Trip.SalesCloseAt = DateTimeOffset.UtcNow.AddHours(1);
        await c.SaveChangesAsync();
        await CommercialHandlerFactory.Booking(c).CreateAsync(CommercialCheckoutTests.Command(seed), default);
        (await c.SeatReservations.CountAsync()).Should().Be(1);
    }

    // ---- Trip chưa có tàu thì không bán được ----

    [Fact]
    public async Task TripWithoutBoat_IsNeverSaleable()
    {
        using var c = TestDbContextFactory.CreateDbContext();
        var seed = TestDbContextFactory.SeedTrip(c);
        seed.Trip.BoatId = null;
        seed.Trip.SalesCloseAt = DateTimeOffset.UtcNow.AddHours(1); // dù mốc đóng bán chưa tới
        await c.SaveChangesAsync();
        c.ChangeTracker.Clear();

        var booking = () => CommercialHandlerFactory.Booking(c).CreateAsync(CommercialCheckoutTests.Command(seed), default);
        await booking.Should().ThrowAsync<ValidationException>();
        (await c.SeatReservations.CountAsync()).Should().Be(0);

        // Sơ đồ ghế của Trip chưa có tàu/inventory hợp lệ bị từ chối hẳn (409), không trả về ghế bán được.
        var seatMap = () => Trips(c, new FixedClock(DateTimeOffset.UtcNow)).GetSeatMapAsync(seed.Trip.Id, SeatMapRequest(seed), default);
        await seatMap.Should().ThrowAsync<ConcurrencyException>();
    }

    // ---- 5. Giá trị cấu hình không hợp lệ không thể đặt mốc sau giờ khởi hành ----

    [Theory]
    [InlineData(-1)]
    [InlineData(-60)]
    [InlineData(1441)]
    public void Validator_RejectsCutoffAfterDepartureOrBeyondOneDay(int minutes)
    {
        var request = new GenerateTripsRequest(Guid.NewGuid(), new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 3), minutes, null);

        new GenerateTripsRequestValidator().Validate(request).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(15)]
    [InlineData(1440)]
    public void Validator_AcceptsOmittedOrBoundedCutoff(int? minutes)
    {
        var request = new GenerateTripsRequest(Guid.NewGuid(), new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 3), minutes, null);

        new GenerateTripsRequestValidator().Validate(request).IsValid.Should().BeTrue();
    }

    /// <summary>Lớp bảo vệ cuối ở DB: CK_Trips_SalesClose từ chối mốc đóng bán sau giờ khởi hành dù có ghi trực tiếp.</summary>
    [Collection(SqlCollections.Serial)]
    public sealed class SalesCloseConstraintSqlTests : IAsyncLifetime
    {
        private string? _connection;
        private ApplicationDbContext Context() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(_connection!).Options);

        public async Task InitializeAsync()
        {
            var configured = Environment.GetEnvironmentVariable("WATERBUS_TEST_SQLSERVER");
            if (string.IsNullOrWhiteSpace(configured)) return;
            _connection = new SqlConnectionStringBuilder(configured) { InitialCatalog = "WaterbusDomainTests_SalesClose_" + Guid.NewGuid().ToString("N") }.ConnectionString;
            await using var c = Context();
            await c.Database.MigrateAsync();
        }

        public async Task DisposeAsync()
        {
            if (_connection is null) return;
            if (!new SqlConnectionStringBuilder(_connection).InitialCatalog.StartsWith("WaterbusDomainTests_SalesClose_")) throw new InvalidOperationException("Unsafe test database.");
            await using var c = Context();
            await c.Database.EnsureDeletedAsync();
        }

        [SqlServerDomainFact]
        public async Task Database_RejectsCutoffAfterDeparture_AndAcceptsDepartureItself()
        {
            await using var seedContext = Context();
            var seed = TestDbContextFactory.SeedTrip(seedContext);

            await using var c = Context();
            var trip = await c.Trips.SingleAsync(t => t.Id == seed.Trip.Id);
            trip.SalesCloseAt = trip.DepartureTime;                 // trần an toàn mặc định: hợp lệ
            await c.SaveChangesAsync();

            trip.SalesCloseAt = trip.DepartureTime.AddMinutes(1);   // sau giờ khởi hành: DB phải từ chối
            var act = () => c.SaveChangesAsync();
            await act.Should().ThrowAsync<DbUpdateException>();
        }
    }
}
