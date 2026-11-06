using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Requests.CheckIns;
using WaterbusSystem.Application.DTOs.Responses.CheckIns;
using WaterbusSystem.Application.Features.CheckIns;
using WaterbusSystem.Application.Features.CheckIns.Services;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.Infrastructure.Repositories;
using WaterbusSystem.UnitTests.Domain;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;

namespace WaterbusSystem.UnitTests.CheckIns;

/// <summary>
/// CHK-02/CHK-03: InMemory không enforce unique index/race timing thật nên 2 bug này cần SQL
/// Server thật (WATERBUS_TEST_SQLSERVER) để chứng minh - theo đúng pattern
/// WeatherGuardConcurrencySqlTests/CommercialSqlTests.
/// </summary>
public sealed class CheckInConcurrencySqlTests : IAsyncLifetime
{
    private readonly Guid _staffId = Guid.NewGuid();
    private string? _connection;
    private ApplicationDbContext Context() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(_connection!).Options);

    public async Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable("WATERBUS_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(configured)) return;
        _connection = new SqlConnectionStringBuilder(configured) { InitialCatalog = "WaterbusDomainTests_CheckIn_" + Guid.NewGuid().ToString("N") }.ConnectionString;
        await using var c = Context();
        await c.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_connection is null) return;
        if (!new SqlConnectionStringBuilder(_connection).InitialCatalog.StartsWith("WaterbusDomainTests_CheckIn_"))
            throw new InvalidOperationException("Unsafe test database.");
        await using var c = Context();
        await c.Database.EnsureDeletedAsync();
    }

    private CheckInService Service(ApplicationDbContext context, DateTimeOffset now)
    {
        var user = new Mock<ICurrentUserService>();
        user.Setup(x => x.UserId).Returns(_staffId.ToString());
        user.Setup(x => x.UserRole).Returns("Staff");

        var clock = new Mock<IDateTimeService>();
        clock.Setup(x => x.Now).Returns(now);

        return new CheckInService(new CheckInRepository(context), new EfUnitOfWork(context),
            user.Object, clock.Object, new CheckInProcessor(new CheckInRepository(context)),
            Options.Create(new NoShowFinalizationOptions()), NullLogger<CheckInService>.Instance);
    }

    [SqlServerDomainFact]
    public async Task ConcurrentSyncOfSameClientEventId_ShouldResultInExactlyOneCheckInEventAndNoUnhandledFailure()
    {
        Guid ticketId, stopCallId;
        DateTimeOffset now;
        var clientEventId = "evt-" + Guid.NewGuid();

        await using (var seed = Context())
        {
            var scenario = CheckInScenarioFactory.SeedCheckInScenario(seed, _staffId);
            ticketId = scenario.Ticket.Id;
            stopCallId = scenario.BoardingCall.Id;
            now = scenario.Now;
        }

        async Task<SyncCheckInEventsResponse> RunSync()
        {
            await using var c = Context();
            var request = new SyncCheckInEventsRequest([new CheckInEventInput(ticketId, stopCallId, clientEventId, now, null)]);
            return await Service(c, now).SyncCheckInEventsAsync(request, CancellationToken.None);
        }

        // Cả 2 request đồng thời gửi CÙNG ClientEventId chưa từng thấy ở server - trước CHK-02,
        // bên thua vi phạm unique index ClientEventId sẽ ném DbUpdateException không được bắt, làm
        // cả batch (dù chỉ 1 event) rơi ra lỗi hạ tầng (500) thay vì trả outcome như lần sync lại.
        var responses = await Task.WhenAll(RunSync(), RunSync());

        responses.Should().HaveCount(2);
        foreach (var response in responses)
        {
            var result = response.Results.Single();
            result.ClientEventId.Should().Be(clientEventId);
            result.Outcome.Should().NotBe("Error", "race ClientEventId phải được xử lý như idempotent replay, không phải lỗi");
        }

        await using var check = Context();
        var events = await check.CheckInEvents.Where(e => e.ClientEventId == clientEventId).ToListAsync();
        events.Should().HaveCount(1, "unique index ClientEventId chỉ cho phép đúng 1 bản ghi dù 2 request đồng thời cùng gửi");

        var ticket = await check.Tickets.SingleAsync(t => t.Id == ticketId);
        ticket.BoardingStatus.Should().Be(BoardingStatus.CheckedIn);
    }

    [SqlServerDomainFact]
    public async Task ConcurrentSyncOfSameClientEventId_WithDifferentTickets_ShouldReportMismatchNotSilentReplay()
    {
        // Review PR #58: đường phục hồi race (catch ConflictException khi vi phạm unique index
        // ClientEventId) phải dùng ĐÚNG quy tắc đối chiếu identity của CHK-06, không được đọc thẳng
        // outcome của bản ghi đã thắng. Dựng 2 Ticket THẬT khác nhau trên cùng 1 TripStopCall, 2
        // request đồng thời cùng gửi 1 ClientEventId nhưng mỗi request mang TicketId riêng - bên
        // thua vi phạm unique index ở DB (không phải lỗi nghiệp vụ pre-check) phải nhận
        // "ClientEventIdMismatch" cho ĐÚNG TicketId của chính nó, không được nhận nhầm outcome
        // "Success" của vé bên thắng.
        Guid ticket1Id, ticket2Id, stopCallId;
        DateTimeOffset now;
        var clientEventId = "evt-race-" + Guid.NewGuid();

        await using (var seed = Context())
        {
            var scenario = CheckInScenarioFactory.SeedCheckInScenario(seed, _staffId);
            ticket1Id = scenario.Ticket.Id;
            stopCallId = scenario.BoardingCall.Id;
            now = scenario.Now;

            var disembarkingCall = scenario.Seed.Trip.TripStopCalls.Single(c => c.VisitOrder == 3);
            var order2 = new PurchaseOrder
            {
                PurchaserName = "Khach Test 2", PurchaserEmail = "khach2@test.com",
                QuotedTotal = TestDbContextFactory.StandardCommuterFare, Status = OrderStatus.Confirmed
            };
            var booking2 = new Booking
            {
                Order = order2, OrderId = order2.Id, BookingCode = Guid.NewGuid().ToString("N"),
                CustomerName = order2.PurchaserName, CustomerEmail = order2.PurchaserEmail,
                QuotedTotal = TestDbContextFactory.StandardCommuterFare, Status = BookingStatus.Confirmed,
                PaidAllocation = TestDbContextFactory.StandardCommuterFare
            };
            booking2.SetJourney(scenario.BoardingCall, disembarkingCall);
            var reservation2 = new SeatReservation
            {
                TripId = scenario.Seed.Trip.Id, SeatId = scenario.Seed.Seat2.Id, BookingId = booking2.Id, Booking = booking2,
                BoardingStopOrder = scenario.BoardingCall.VisitOrder, DisembarkingStopOrder = disembarkingCall.VisitOrder,
                Status = ReservationStatus.Confirmed, PassengerName = "Tran Thi B",
                QuotedFare = TestDbContextFactory.StandardCommuterFare, PaidAllocation = TestDbContextFactory.StandardCommuterFare,
                SeatCodeAtSale = scenario.Seed.Seat2.SeatCode, SeatClassAtSale = scenario.Seed.StandardClass.Name
            };
            booking2.SeatReservations.Add(reservation2);
            var payment2 = new FinancialTransaction
            {
                OrderId = order2.Id, Order = order2,
                // IdempotencyKey mặc định "" (FinancialTransaction.cs) đụng unique index
                // IX_FinancialTransactions_IdempotencyKey với FinancialTransaction của ticket1 (cùng
                // SeedCheckInScenario cũng để trống) - set riêng để 2 FinancialTransaction thật trong
                // CÙNG 1 DB test này không đụng nhau (lỗi này chỉ lộ ra trên SQL Server thật, không
                // phải lỗi của fix CHK-06/CHK-02 đang test).
                IdempotencyKey = "idem-" + Guid.NewGuid(),
                Status = FinancialTransactionStatus.Succeeded, Amount = TestDbContextFactory.StandardCommuterFare
            };
            var ticket2 = Ticket.Issue(booking2, reservation2, payment2, TripType.Commuter, DateTimeOffset.UtcNow);
            booking2.Tickets.Add(ticket2);

            seed.PurchaseOrders.Add(order2);
            seed.Bookings.Add(booking2);
            seed.SeatReservations.Add(reservation2);
            seed.FinancialTransactions.Add(payment2);
            seed.Tickets.Add(ticket2);
            await seed.SaveChangesAsync();
            ticket2Id = ticket2.Id;
        }

        async Task<CheckInEventSyncResultResponse> RunSync(Guid ticketId)
        {
            await using var c = Context();
            var request = new SyncCheckInEventsRequest([new CheckInEventInput(ticketId, stopCallId, clientEventId, now, null)]);
            var response = await Service(c, now).SyncCheckInEventsAsync(request, CancellationToken.None);
            return response.Results.Single();
        }

        var results = await Task.WhenAll(RunSync(ticket1Id), RunSync(ticket2Id));
        var result1 = results[0];
        var result2 = results[1];

        new[] { result1.Outcome, result2.Outcome }.Should().BeEquivalentTo(new[] { "Success", "ClientEventIdMismatch" },
            "đúng 1 trong 2 Ticket thắng race (Success), bên kia phải bị từ chối vì ClientEventId không khớp Ticket của chính nó");

        await using var check = Context();
        var events = await check.CheckInEvents.Where(e => e.ClientEventId == clientEventId).ToListAsync();
        events.Should().HaveCount(1, "unique index ClientEventId chỉ cho phép đúng 1 bản ghi dù 2 Ticket khác nhau cùng gửi key này");

        var checkedInTicketId = result1.Outcome == "Success" ? ticket1Id : ticket2Id;
        var mismatchedTicketId = result1.Outcome == "Success" ? ticket2Id : ticket1Id;
        (await check.Tickets.SingleAsync(t => t.Id == checkedInTicketId)).BoardingStatus.Should().Be(BoardingStatus.CheckedIn);
        (await check.Tickets.SingleAsync(t => t.Id == mismatchedTicketId)).BoardingStatus.Should().Be(BoardingStatus.NotCheckedIn,
            "vé bị ClientEventIdMismatch không được âm thầm check-in theo kết quả của vé khác");
    }

    [SqlServerDomainFact]
    public async Task ConcurrentPrimaryOfflineScannerAssignment_ShouldLeaveExactlyOneOpenEndedPrimary()
    {
        Guid stopCallId;
        Guid staffA = Guid.NewGuid(), staffB = Guid.NewGuid();
        Guid deviceAId, deviceBId;
        DateTimeOffset activeFrom;

        await using (var seed = Context())
        {
            var scenario = CheckInScenarioFactory.SeedCheckInScenario(seed, _staffId, withAssignment: false);
            stopCallId = scenario.BoardingCall.Id;
            activeFrom = scenario.Now.AddHours(-1);

            var deviceA = new ScannerDevice { DeviceCode = "DEV-RACE-A", IsActive = true };
            var deviceB = new ScannerDevice { DeviceCode = "DEV-RACE-B", IsActive = true };
            seed.ScannerDevices.AddRange(deviceA, deviceB);
            await seed.SaveChangesAsync();
            deviceAId = deviceA.Id;
            deviceBId = deviceB.Id;
        }

        async Task AssignAsync(Guid deviceId, Guid staffId)
        {
            await using var c = Context();
            var request = new AssignScannerRequest(deviceId, staffId, stopCallId, true, activeFrom, null);
            await Service(c, activeFrom).AssignScannerAsync(request, CancellationToken.None);
        }

        // Hai request đồng thời cùng gán Primary Offline Scanner (không ActiveUntil) cho ĐÚNG 1
        // điểm dừng - HasOverlappingPrimaryAssignmentAsync (check-then-insert) không tự chặn được
        // race này; unique filtered index ở DB (CHK-03) phải là lưới chặn cuối cùng. Một trong hai
        // request được PHÉP ném ConflictException (giống hành vi pre-check hiện có khi làm tuần tự).
        var results = await Task.WhenAll(
            SafeRun(() => AssignAsync(deviceAId, staffA)),
            SafeRun(() => AssignAsync(deviceBId, staffB)));

        results.Count(r => r).Should().Be(1, "chỉ đúng 1 trong 2 request đồng thời được phép tạo Primary Offline Scanner mới");

        await using var check = Context();
        var openEndedPrimaries = await check.ScannerAssignments
            .Where(a => a.TripStopCallId == stopCallId && a.IsPrimaryOffline && a.ActiveUntil == null && !a.IsDeleted)
            .ToListAsync();
        openEndedPrimaries.Should().HaveCount(1, "unique filtered index phải chặn cứng 2 Primary Offline Scanner vô thời hạn cùng tồn tại cho 1 điểm dừng");
    }

    private static async Task<bool> SafeRun(Func<Task> action)
    {
        try
        {
            await action();
            return true;
        }
        catch (WaterbusSystem.Domain.Exceptions.ConflictException)
        {
            return false;
        }
    }
}
