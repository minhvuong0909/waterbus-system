using WaterbusSystem.Application.Common.Models;
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
using WaterbusSystem.Application.Interfaces.Repositories;
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

    /// <summary>
    /// Review PR #58: đồng bộ các task test NGAY TRƯỚC lệnh gọi service thực sự, thay vì chỉ dựa
    /// vào Task.WhenAll khởi động "gần như cùng lúc" - Task.WhenAll không đảm bảo cả 2 task đã mở
    /// xong DbContext/dựng xong request trước khi bên kia bắt đầu gọi service; nếu 1 bên vô tình
    /// chạy trọn vẹn (kể cả SaveChangesAsync) trước khi bên kia bắt đầu, bên sau chỉ đơn giản thấy
    /// existingEvent qua pre-check (nhánh KHÔNG hề chạm unique index ở DB) chứ không phải đúng race
    /// đang muốn chứng minh (2 request cùng pass pre-check rồi cùng insert, 1 thắng 1 thua ở DB).
    /// Arrive() chỉ trả về khi ĐỦ `parties` task đã gọi, đảm bảo khởi động đồng thời thật.
    /// </summary>
    private sealed class StartGate(int parties)
    {
        private int _arrived;
        private readonly TaskCompletionSource _gate = new();

        public Task Arrive()
        {
            if (Interlocked.Increment(ref _arrived) == parties) _gate.SetResult();
            return _gate.Task;
        }
    }

    /// <summary>
    /// Review PR #58 (2nd round - "Concurrency regression tests must exercise the actual database
    /// race"): rào StartGate đặt NGAY TRƯỚC lệnh gọi service (Service(...).SyncCheckInEventsAsync)
    /// vẫn chưa đủ - đó là TRƯỚC bước FindEventByClientIdAsync (pre-check CHK-02/CHK-06 thật diễn
    /// ra bên trong CheckInProcessor.ProcessAsync). 1 trong 2 task hoàn toàn có thể chạy xong (kể cả
    /// SaveChangesAsync) TRƯỚC KHI task kia bắt đầu lookup - khi đó task sau chỉ đơn giản thấy
    /// existingEvent qua pre-check bình thường (nhánh KHÔNG đụng unique index DB), không phải race
    /// thật đang cần chứng minh. Decorator test-only này đồng bộ NGAY SAU khi CẢ 2 task đã gọi
    /// FindEventByClientIdAsync (đều thấy null, vì chưa ai insert) - buộc cả 2 cùng tiến vào bước
    /// insert/SaveChangesAsync, nên 1 trong 2 phải chạm đúng unique index violation ở DB
    /// (ConflictException từ EfUnitOfWork) thay vì nhánh pre-check thường. KHÔNG đổi gì ở
    /// CheckInProcessor/CheckInRepository production - chỉ decorate interface ở tầng test.
    /// </summary>
    private sealed class LookupGatedCheckInRepository(ICheckInRepository inner, StartGate gate) : ICheckInRepository
    {
        public async Task<CheckInEvent?> FindEventByClientIdAsync(string clientEventId, CancellationToken cancellationToken = default)
        {
            var result = await inner.FindEventByClientIdAsync(clientEventId, cancellationToken);
            await gate.Arrive();
            return result;
        }

        public Task<bool> IsEligibleScannerStaffAsync(Guid staffId, CancellationToken cancellationToken = default) =>
            inner.IsEligibleScannerStaffAsync(staffId, cancellationToken);

        public Task<PaginatedList<ScannerStaffResponse>> ListScannerStaffAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default) =>
            inner.ListScannerStaffAsync(pageNumber, pageSize, cancellationToken);

        public Task<PaginatedList<ScannerAssignmentAdminResponse>> ListScannerAssignmentsAsync(
            Guid tripStopCallId, DateTimeOffset now, int pageNumber, int pageSize, CancellationToken cancellationToken = default) =>
            inner.ListScannerAssignmentsAsync(tripStopCallId, now, pageNumber, pageSize, cancellationToken);

        public Task<ScannerDevice> GetRequiredDeviceAsync(Guid deviceId, CancellationToken cancellationToken = default) =>
            inner.GetRequiredDeviceAsync(deviceId, cancellationToken);
        public Task<TripStopCall> GetRequiredStopCallAsync(Guid tripStopCallId, CancellationToken cancellationToken = default) =>
            inner.GetRequiredStopCallAsync(tripStopCallId, cancellationToken);
        public Task<bool> HasOverlappingPrimaryAssignmentAsync(Guid tripStopCallId, DateTimeOffset activeFrom, DateTimeOffset requestedEnd, CancellationToken cancellationToken = default) =>
            inner.HasOverlappingPrimaryAssignmentAsync(tripStopCallId, activeFrom, requestedEnd, cancellationToken);
        public void AddAssignment(ScannerAssignment assignment) => inner.AddAssignment(assignment);
        public Task<ScannerAssignment> GetRequiredAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken = default) =>
            inner.GetRequiredAssignmentAsync(assignmentId, cancellationToken);
        public Task<bool> HasActiveAssignmentAsync(Guid staffId, Guid tripStopCallId, DateTimeOffset now, CancellationToken cancellationToken = default) =>
            inner.HasActiveAssignmentAsync(staffId, tripStopCallId, now, cancellationToken);
        public Task<List<MyScannerAssignmentResponse>> ListMyAssignmentsAsync(Guid staffId, DateTimeOffset now, CancellationToken cancellationToken = default) =>
            inner.ListMyAssignmentsAsync(staffId, now, cancellationToken);
        public Task<Ticket?> GetTicketWithReservationAsync(Guid ticketId, CancellationToken cancellationToken = default) =>
            inner.GetTicketWithReservationAsync(ticketId, cancellationToken);
        public Task<Ticket> GetRequiredTicketWithBookingAsync(Guid ticketId, CancellationToken cancellationToken = default) =>
            inner.GetRequiredTicketWithBookingAsync(ticketId, cancellationToken);
        public void AddCheckInEvent(CheckInEvent checkInEvent) => inner.AddCheckInEvent(checkInEvent);
        public Task<TripStopCall> GetStopCallForManifestAsync(Guid tripStopCallId, CancellationToken cancellationToken = default) =>
            inner.GetStopCallForManifestAsync(tripStopCallId, cancellationToken);
        public Task<List<Ticket>> ListTicketsForManifestAsync(Guid tripStopCallId, CancellationToken cancellationToken = default) =>
            inner.ListTicketsForManifestAsync(tripStopCallId, cancellationToken);
        public Task<List<Guid>> ListClosedStopCallIdsAsync(DateTimeOffset deadline, CancellationToken cancellationToken = default) =>
            inner.ListClosedStopCallIdsAsync(deadline, cancellationToken);
        public Task<List<Guid>> ListUnresolvedStopCallIdsAsync(IReadOnlyCollection<Guid> stopCallIds, CancellationToken cancellationToken = default) =>
            inner.ListUnresolvedStopCallIdsAsync(stopCallIds, cancellationToken);
        public Task<List<Ticket>> ListNoShowCandidateTicketsAsync(IReadOnlyCollection<Guid> eligibleStopCallIds, CancellationToken cancellationToken = default) =>
            inner.ListNoShowCandidateTicketsAsync(eligibleStopCallIds, cancellationToken);
    }

    private CheckInService Service(
        ApplicationDbContext context, DateTimeOffset now, ICheckInRepository? repositoryOverride = null, Guid? staffIdOverride = null)
    {
        var user = new Mock<ICurrentUserService>();
        user.Setup(x => x.UserId).Returns((staffIdOverride ?? _staffId).ToString());
        user.Setup(x => x.UserRole).Returns("Staff");

        var clock = new Mock<IDateTimeService>();
        clock.Setup(x => x.Now).Returns(now);

        var repository = repositoryOverride ?? new CheckInRepository(context);
        return new CheckInService(repository, new EfUnitOfWork(context),
            user.Object, clock.Object, new CheckInProcessor(repository),
            new SqlScannerAssignmentLock(context),
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

        var gate = new StartGate(2);
        async Task<SyncCheckInEventsResponse> RunSync()
        {
            await using var c = Context();
            var request = new SyncCheckInEventsRequest([new CheckInEventInput(ticketId, stopCallId, clientEventId, now, null)]);
            // Đồng bộ NGAY TẠI biên FindEventByClientIdAsync (xem LookupGatedCheckInRepository) -
            // không phải trước lệnh gọi service - để chắc chắn chạm đúng race ở DB.
            var service = Service(c, now, new LookupGatedCheckInRepository(new CheckInRepository(c), gate));
            return await service.SyncCheckInEventsAsync(request, CancellationToken.None);
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

        var gate = new StartGate(2);
        async Task<CheckInEventSyncResultResponse> RunSync(Guid ticketId)
        {
            await using var c = Context();
            var request = new SyncCheckInEventsRequest([new CheckInEventInput(ticketId, stopCallId, clientEventId, now, null)]);
            // Đồng bộ NGAY TẠI biên FindEventByClientIdAsync (xem LookupGatedCheckInRepository) -
            // không phải trước lệnh gọi service - để chắc chắn chạm đúng race ở DB.
            var service = Service(c, now, new LookupGatedCheckInRepository(new CheckInRepository(c), gate));
            var response = await service.SyncCheckInEventsAsync(request, CancellationToken.None);
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
    public async Task ConcurrentSyncOfSameClientEventId_WithDifferentStaff_ShouldReportMismatchNotSilentReplay()
    {
        // Review (human, commit fc2fc74): "kiểm tra cả trường hợp trùng ClientEventId nhưng khác
        // Staff" - test trước đã phủ khác Ticket, test này phủ khác Staff. Cùng 1 Ticket/Stop,
        // nhưng 2 NHÂN VIÊN khác nhau cùng gửi 1 ClientEventId - bên thua vi phạm unique index ở
        // DB phải nhận "ClientEventIdMismatch" cho ĐÚNG staff của chính nó, không được đọc nhầm
        // outcome của Staff kia (ai thực sự đã check-in vé này phải đúng với CheckedInByStaffId).
        Guid ticketId, stopCallId;
        DateTimeOffset now;
        Guid staffA = Guid.NewGuid(), staffB = Guid.NewGuid();
        var clientEventId = "evt-staff-race-" + Guid.NewGuid();

        await using (var seed = Context())
        {
            var scenario = CheckInScenarioFactory.SeedCheckInScenario(seed, staffA, withAssignment: false);
            ticketId = scenario.Ticket.Id;
            stopCallId = scenario.BoardingCall.Id;
            now = scenario.Now;

            var deviceA = new ScannerDevice { DeviceCode = "DEV-STAFF-A", IsActive = true };
            var deviceB = new ScannerDevice { DeviceCode = "DEV-STAFF-B", IsActive = true };
            seed.ScannerDevices.AddRange(deviceA, deviceB);
            seed.ScannerAssignments.AddRange(
                new ScannerAssignment { DeviceId = deviceA.Id, StaffAccountId = staffA, TripStopCallId = stopCallId, IsPrimaryOffline = true, ActiveFrom = now.AddHours(-1), ActiveUntil = now.AddHours(1) },
                new ScannerAssignment { DeviceId = deviceB.Id, StaffAccountId = staffB, TripStopCallId = stopCallId, IsPrimaryOffline = false, ActiveFrom = now.AddHours(-1), ActiveUntil = now.AddHours(1) });
            await seed.SaveChangesAsync();
        }

        var gate = new StartGate(2);
        async Task<CheckInEventSyncResultResponse> RunSync(Guid staffId)
        {
            await using var c = Context();
            var request = new SyncCheckInEventsRequest([new CheckInEventInput(ticketId, stopCallId, clientEventId, now, null)]);
            var service = Service(c, now, new LookupGatedCheckInRepository(new CheckInRepository(c), gate), staffId);
            var response = await service.SyncCheckInEventsAsync(request, CancellationToken.None);
            return response.Results.Single();
        }

        var results = await Task.WhenAll(RunSync(staffA), RunSync(staffB));

        new[] { results[0].Outcome, results[1].Outcome }.Should().BeEquivalentTo(new[] { "Success", "ClientEventIdMismatch" },
            "đúng 1 trong 2 Staff thắng race (Success), bên kia phải bị từ chối vì ClientEventId không khớp Staff của chính nó");

        await using var check = Context();
        var events = await check.CheckInEvents.Where(e => e.ClientEventId == clientEventId).ToListAsync();
        events.Should().HaveCount(1, "unique index ClientEventId chỉ cho phép đúng 1 bản ghi dù 2 Staff khác nhau cùng gửi key này");

        var winningStaffId = results[0].Outcome == "Success" ? staffA : staffB;
        var ticket = await check.Tickets.SingleAsync(t => t.Id == ticketId);
        ticket.BoardingStatus.Should().Be(BoardingStatus.CheckedIn);
        ticket.CheckedInByStaffId.Should().Be(winningStaffId,
            "CheckedInByStaffId phải đúng là Staff đã thắng race - không được ghi nhận cho Staff bị ClientEventIdMismatch");
    }

    [SqlServerDomainFact]
    public async Task ConcurrentSyncOfSameClientEventId_WithDifferentStop_ShouldReportMismatchNotSilentReplay()
    {
        // Review (human, commit fc2fc74): "kiểm tra cả trường hợp trùng ClientEventId nhưng khác
        // Stop" - cùng 1 Ticket/Staff, nhưng 2 ĐIỂM DỪNG khác nhau cùng gửi 1 ClientEventId (vd bug
        // client gửi cùng key cho 2 lần quét ở 2 bến khác nhau). Bên thua vi phạm unique index phải
        // nhận "ClientEventIdMismatch" cho ĐÚNG điểm dừng của chính nó.
        Guid ticketId, stopCall1Id, stopCall2Id;
        DateTimeOffset now;
        var clientEventId = "evt-stop-race-" + Guid.NewGuid();

        await using (var seed = Context())
        {
            var scenario = CheckInScenarioFactory.SeedCheckInScenario(seed, _staffId, withAssignment: false);
            ticketId = scenario.Ticket.Id;
            stopCall1Id = scenario.BoardingCall.Id;
            now = scenario.Now;
            stopCall2Id = scenario.Seed.Trip.TripStopCalls.Single(c => c.VisitOrder == 2).Id;

            var device1 = new ScannerDevice { DeviceCode = "DEV-STOP-1", IsActive = true };
            var device2 = new ScannerDevice { DeviceCode = "DEV-STOP-2", IsActive = true };
            seed.ScannerDevices.AddRange(device1, device2);
            seed.ScannerAssignments.AddRange(
                new ScannerAssignment { DeviceId = device1.Id, StaffAccountId = _staffId, TripStopCallId = stopCall1Id, IsPrimaryOffline = true, ActiveFrom = now.AddHours(-1), ActiveUntil = now.AddHours(1) },
                new ScannerAssignment { DeviceId = device2.Id, StaffAccountId = _staffId, TripStopCallId = stopCall2Id, IsPrimaryOffline = true, ActiveFrom = now.AddHours(-1), ActiveUntil = now.AddHours(1) });
            await seed.SaveChangesAsync();
        }

        var gate = new StartGate(2);
        async Task<CheckInEventSyncResultResponse> RunSync(Guid stopCallId)
        {
            await using var c = Context();
            var request = new SyncCheckInEventsRequest([new CheckInEventInput(ticketId, stopCallId, clientEventId, now, null)]);
            var service = Service(c, now, new LookupGatedCheckInRepository(new CheckInRepository(c), gate));
            var response = await service.SyncCheckInEventsAsync(request, CancellationToken.None);
            return response.Results.Single();
        }

        var results = await Task.WhenAll(RunSync(stopCall1Id), RunSync(stopCall2Id));

        results.Count(r => r.Outcome == "ClientEventIdMismatch").Should().Be(1,
            "đúng 1 trong 2 điểm dừng thắng race, bên kia phải bị từ chối vì ClientEventId không khớp TripStopCallId của chính nó");

        await using var check = Context();
        var events = await check.CheckInEvents.Where(e => e.ClientEventId == clientEventId).ToListAsync();
        events.Should().HaveCount(1, "unique index ClientEventId chỉ cho phép đúng 1 bản ghi dù 2 điểm dừng khác nhau cùng gửi key này");
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
            TestDbContextFactory.AddScannerStaff(seed, staffA);
            TestDbContextFactory.AddScannerStaff(seed, staffB);
            stopCallId = scenario.BoardingCall.Id;
            activeFrom = scenario.Now.AddHours(-1);

            var deviceA = new ScannerDevice { DeviceCode = "DEV-RACE-A", IsActive = true };
            var deviceB = new ScannerDevice { DeviceCode = "DEV-RACE-B", IsActive = true };
            seed.ScannerDevices.AddRange(deviceA, deviceB);
            await seed.SaveChangesAsync();
            deviceAId = deviceA.Id;
            deviceBId = deviceB.Id;
        }

        var gate = new StartGate(2);
        async Task AssignAsync(Guid deviceId, Guid staffId)
        {
            await using var c = Context();
            var request = new AssignScannerRequest(deviceId, staffId, stopCallId, true, activeFrom, null);
            var service = Service(c, activeFrom);
            await gate.Arrive();
            await service.AssignScannerAsync(request, CancellationToken.None);
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

    [SqlServerDomainFact]
    public async Task ConcurrentOverlappingFinitePrimaryAssignments_ShouldLeaveExactlyOneSucceeding()
    {
        // Review PR #58 (High - "Enforce the complete Primary Offline Scanner overlap invariant"):
        // unique filtered index (CHK-03) chỉ phủ case ActiveUntil IS NULL (vô thời hạn) - 2 Primary
        // Offline Scanner HỮU HẠN (có ActiveUntil) chồng lấn giờ KHÔNG được index đó bảo vệ, DB vẫn
        // cho phép cả 2 insert nếu chỉ dựa vào check-then-insert. Test này chứng minh
        // IScannerAssignmentLock (sp_getapplock theo TripStopCallId, giữ suốt từ lúc kiểm tra
        // overlap tới khi SaveChangesAsync commit/rollback) đóng đúng khoảng hở này bằng khoá thật,
        // không phải unique index.
        Guid stopCallId;
        Guid staffA = Guid.NewGuid(), staffB = Guid.NewGuid();
        Guid deviceAId, deviceBId;
        DateTimeOffset activeFrom;

        await using (var seed = Context())
        {
            var scenario = CheckInScenarioFactory.SeedCheckInScenario(seed, _staffId, withAssignment: false);
            TestDbContextFactory.AddScannerStaff(seed, staffA);
            TestDbContextFactory.AddScannerStaff(seed, staffB);
            stopCallId = scenario.BoardingCall.Id;
            activeFrom = scenario.Now.AddHours(-1);

            var deviceA = new ScannerDevice { DeviceCode = "DEV-FIN-A", IsActive = true };
            var deviceB = new ScannerDevice { DeviceCode = "DEV-FIN-B", IsActive = true };
            seed.ScannerDevices.AddRange(deviceA, deviceB);
            await seed.SaveChangesAsync();
            deviceAId = deviceA.Id;
            deviceBId = deviceB.Id;
        }

        var gate = new StartGate(2);
        // 2 khoảng hữu hạn CHỒNG LẤN giờ: A = [activeFrom, activeFrom+2h), B = [activeFrom+1h, activeFrom+3h).
        async Task AssignAsync(Guid deviceId, Guid staffId, DateTimeOffset from, DateTimeOffset until)
        {
            await using var c = Context();
            var request = new AssignScannerRequest(deviceId, staffId, stopCallId, true, from, until);
            var service = Service(c, from);
            await gate.Arrive();
            await service.AssignScannerAsync(request, CancellationToken.None);
        }

        var results = await Task.WhenAll(
            SafeRun(() => AssignAsync(deviceAId, staffA, activeFrom, activeFrom.AddHours(2))),
            SafeRun(() => AssignAsync(deviceBId, staffB, activeFrom.AddHours(1), activeFrom.AddHours(3))));

        results.Count(r => r).Should().Be(1,
            "2 Primary Offline Scanner hữu hạn chồng lấn giờ đồng thời - đúng 1 request được phép thành công nhờ sp_getapplock theo TripStopCallId, không phải unique index (index không phủ case này)");

        await using var check = Context();
        var primariesAtStop = await check.ScannerAssignments
            .Where(a => a.TripStopCallId == stopCallId && a.IsPrimaryOffline && !a.IsDeleted)
            .ToListAsync();
        primariesAtStop.Should().HaveCount(1, "request thua phải bị chặn hoàn toàn trong khoá (ConflictException từ pre-check), không để lại bản ghi chồng lấn nào");
    }

    [SqlServerDomainFact]
    public async Task ConcurrentNonOverlappingFinitePrimaryAssignments_ShouldBothSucceed()
    {
        // Review (human, commit fc2fc74): khoá sp_getapplock SERIALIZE 2 request cùng TripStopCallId
        // (chạy tuần tự bên trong khoá), nhưng KHÔNG được biến thành "chỉ 1 được phép thành công vô
        // điều kiện" - request thứ 2 chạy SAU vẫn phải được chấp nhận nếu khoảng thời gian của nó
        // KHÔNG chồng lấn với request đã commit trước. Test này chứng minh khoá chỉ serialize thứ
        // tự thực thi, không tự ý biến request hợp lệ thành ConflictException.
        Guid stopCallId;
        Guid staffA = Guid.NewGuid(), staffB = Guid.NewGuid();
        Guid deviceAId, deviceBId;
        DateTimeOffset activeFrom;

        await using (var seed = Context())
        {
            var scenario = CheckInScenarioFactory.SeedCheckInScenario(seed, _staffId, withAssignment: false);
            TestDbContextFactory.AddScannerStaff(seed, staffA);
            TestDbContextFactory.AddScannerStaff(seed, staffB);
            stopCallId = scenario.BoardingCall.Id;
            activeFrom = scenario.Now.AddHours(-1);

            var deviceA = new ScannerDevice { DeviceCode = "DEV-NOV-A", IsActive = true };
            var deviceB = new ScannerDevice { DeviceCode = "DEV-NOV-B", IsActive = true };
            seed.ScannerDevices.AddRange(deviceA, deviceB);
            await seed.SaveChangesAsync();
            deviceAId = deviceA.Id;
            deviceBId = deviceB.Id;
        }

        var gate = new StartGate(2);
        // A = [activeFrom, activeFrom+1h), B = [activeFrom+2h, activeFrom+3h) - cách xa nhau, không chồng lấn.
        async Task AssignAsync(Guid deviceId, Guid staffId, DateTimeOffset from, DateTimeOffset until)
        {
            await using var c = Context();
            var request = new AssignScannerRequest(deviceId, staffId, stopCallId, true, from, until);
            var service = Service(c, from);
            await gate.Arrive();
            await service.AssignScannerAsync(request, CancellationToken.None);
        }

        var results = await Task.WhenAll(
            SafeRun(() => AssignAsync(deviceAId, staffA, activeFrom, activeFrom.AddHours(1))),
            SafeRun(() => AssignAsync(deviceBId, staffB, activeFrom.AddHours(2), activeFrom.AddHours(3))));

        results.Should().AllSatisfy(r => r.Should().BeTrue(
            "2 Primary Offline Scanner hữu hạn KHÔNG chồng lấn giờ phải đều được chấp nhận dù khoá serialize thứ tự thực thi của chúng"));

        await using var check = Context();
        (await check.ScannerAssignments.Where(a => a.TripStopCallId == stopCallId && a.IsPrimaryOffline && !a.IsDeleted).CountAsync())
            .Should().Be(2, "cả 2 phân công hợp lệ, không chồng lấn đều phải tồn tại");
    }

    [SqlServerDomainFact]
    public async Task ConcurrentAdjacentFinitePrimaryAssignments_ShouldBothSucceed()
    {
        // Review (human, commit fc2fc74): "hai khoảng liền kề" - A kết thúc ĐÚNG lúc B bắt đầu
        // ([ActiveFrom, ActiveUntil) nửa mở theo đúng semantics hiện có của
        // HasOverlappingPrimaryAssignmentAsync: activeFrom < requestedEnd && (ActiveUntil ??
        // MaxValue) > activeFrom) - đây KHÔNG được coi là chồng lấn, cả 2 phải thành công.
        Guid stopCallId;
        Guid staffA = Guid.NewGuid(), staffB = Guid.NewGuid();
        Guid deviceAId, deviceBId;
        DateTimeOffset activeFrom;

        await using (var seed = Context())
        {
            var scenario = CheckInScenarioFactory.SeedCheckInScenario(seed, _staffId, withAssignment: false);
            TestDbContextFactory.AddScannerStaff(seed, staffA);
            TestDbContextFactory.AddScannerStaff(seed, staffB);
            stopCallId = scenario.BoardingCall.Id;
            activeFrom = scenario.Now.AddHours(-1);

            var deviceA = new ScannerDevice { DeviceCode = "DEV-ADJ-A", IsActive = true };
            var deviceB = new ScannerDevice { DeviceCode = "DEV-ADJ-B", IsActive = true };
            seed.ScannerDevices.AddRange(deviceA, deviceB);
            await seed.SaveChangesAsync();
            deviceAId = deviceA.Id;
            deviceBId = deviceB.Id;
        }

        var boundary = activeFrom.AddHours(1);
        var gate = new StartGate(2);
        // A = [activeFrom, boundary), B = [boundary, boundary+1h) - liền kề đúng tại boundary.
        async Task AssignAsync(Guid deviceId, Guid staffId, DateTimeOffset from, DateTimeOffset until)
        {
            await using var c = Context();
            var request = new AssignScannerRequest(deviceId, staffId, stopCallId, true, from, until);
            var service = Service(c, from);
            await gate.Arrive();
            await service.AssignScannerAsync(request, CancellationToken.None);
        }

        var results = await Task.WhenAll(
            SafeRun(() => AssignAsync(deviceAId, staffA, activeFrom, boundary)),
            SafeRun(() => AssignAsync(deviceBId, staffB, boundary, boundary.AddHours(1))));

        results.Should().AllSatisfy(r => r.Should().BeTrue(
            "2 khoảng LIỀN KỀ (A kết thúc đúng lúc B bắt đầu) không phải chồng lấn theo semantics nửa mở hiện có - cả 2 phải được chấp nhận"));

        await using var check = Context();
        (await check.ScannerAssignments.Where(a => a.TripStopCallId == stopCallId && a.IsPrimaryOffline && !a.IsDeleted).CountAsync())
            .Should().Be(2, "cả 2 phân công liền kề đều phải tồn tại");
    }

    [SqlServerDomainFact]
    public async Task RevokeThenReassignPrimaryOnSameStop_ShouldSucceedOnRealSqlServer()
    {
        // Review (human, commit fc2fc74): "thu hồi rồi gán lại" trên SQL Server thật (không chỉ
        // InMemory) - unique filtered index (ActiveUntil IS NULL) phải hết áp dụng cho bản ghi cũ
        // NGAY SAU khi RevokeScannerAssignmentAsync gán ActiveUntil, để sp_getapplock + pre-check
        // không chặn nhầm 1 phân công Primary vô thời hạn MỚI hợp lệ cho đúng điểm dừng đó.
        Guid stopCallId, oldAssignmentId;
        Guid staffOld = Guid.NewGuid(), staffNew = Guid.NewGuid();
        DateTimeOffset activeFrom;

        await using (var seed = Context())
        {
            var scenario = CheckInScenarioFactory.SeedCheckInScenario(seed, _staffId, withAssignment: false);
            TestDbContextFactory.AddScannerStaff(seed, staffOld);
            TestDbContextFactory.AddScannerStaff(seed, staffNew);
            stopCallId = scenario.BoardingCall.Id;
            activeFrom = scenario.Now.AddHours(-1);

            var deviceOld = new ScannerDevice { DeviceCode = "DEV-REVOKE-OLD", IsActive = true };
            seed.ScannerDevices.Add(deviceOld);
            await seed.SaveChangesAsync();

            var service = Service(seed, activeFrom);
            var assigned = await service.AssignScannerAsync(
                new AssignScannerRequest(deviceOld.Id, staffOld, stopCallId, true, activeFrom, null), CancellationToken.None);
            oldAssignmentId = assigned.AssignmentId;
        }

        await using (var revokeContext = Context())
        {
            var service = Service(revokeContext, activeFrom.AddMinutes(30));
            await service.RevokeScannerAssignmentAsync(oldAssignmentId, CancellationToken.None);
        }

        Guid newDeviceId;
        await using (var seed2 = Context())
        {
            var deviceNew = new ScannerDevice { DeviceCode = "DEV-REVOKE-NEW", IsActive = true };
            seed2.ScannerDevices.Add(deviceNew);
            await seed2.SaveChangesAsync();
            newDeviceId = deviceNew.Id;
        }

        await using var reassignContext = Context();
        var reassignService = Service(reassignContext, activeFrom.AddMinutes(31));
        var act = () => reassignService.AssignScannerAsync(
            new AssignScannerRequest(newDeviceId, staffNew, stopCallId, true, activeFrom.AddMinutes(31), null), CancellationToken.None);

        await act.Should().NotThrowAsync("sau khi thu hồi, điểm dừng này phải nhận lại được 1 Primary Offline Scanner vô thời hạn mới ngay");

        await using var check = Context();
        var openEndedPrimaries = await check.ScannerAssignments
            .Where(a => a.TripStopCallId == stopCallId && a.IsPrimaryOffline && a.ActiveUntil == null && !a.IsDeleted)
            .ToListAsync();
        openEndedPrimaries.Should().HaveCount(1, "chỉ đúng phân công MỚI còn vô thời hạn - phân công cũ đã bị Revoke (có ActiveUntil) không còn khớp filter của index");
        openEndedPrimaries.Single().StaffAccountId.Should().Be(staffNew);
    }

    [SqlServerDomainFact]
    public async Task ConcurrentPrimaryAndSecondaryAssignment_ShouldBothSucceed()
    {
        // Review (human, commit fc2fc74): "Secondary assignment" dưới tải đồng thời - Secondary
        // KHÔNG tham gia bất biến "chỉ 1 Primary" (AssignScannerAsync chỉ acquire khoá khi
        // IsPrimaryOffline=true) nên phải luôn thành công cùng lúc với 1 Primary đang được gán cho
        // ĐÚNG điểm dừng đó, không bị chặn/chờ khoá của Primary.
        Guid stopCallId;
        Guid staffPrimary = Guid.NewGuid(), staffSecondary = Guid.NewGuid();
        Guid devicePrimaryId, deviceSecondaryId;
        DateTimeOffset activeFrom;

        await using (var seed = Context())
        {
            var scenario = CheckInScenarioFactory.SeedCheckInScenario(seed, _staffId, withAssignment: false);
            TestDbContextFactory.AddScannerStaff(seed, staffPrimary);
            TestDbContextFactory.AddScannerStaff(seed, staffSecondary);
            stopCallId = scenario.BoardingCall.Id;
            activeFrom = scenario.Now.AddHours(-1);

            var devicePrimary = new ScannerDevice { DeviceCode = "DEV-SEC-PRIMARY", IsActive = true };
            var deviceSecondary = new ScannerDevice { DeviceCode = "DEV-SEC-SECONDARY", IsActive = true };
            seed.ScannerDevices.AddRange(devicePrimary, deviceSecondary);
            await seed.SaveChangesAsync();
            devicePrimaryId = devicePrimary.Id;
            deviceSecondaryId = deviceSecondary.Id;
        }

        var gate = new StartGate(2);
        async Task AssignAsync(Guid deviceId, Guid staffId, bool isPrimary)
        {
            await using var c = Context();
            var request = new AssignScannerRequest(deviceId, staffId, stopCallId, isPrimary, activeFrom, activeFrom.AddHours(2));
            var service = Service(c, activeFrom);
            await gate.Arrive();
            await service.AssignScannerAsync(request, CancellationToken.None);
        }

        var results = await Task.WhenAll(
            SafeRun(() => AssignAsync(devicePrimaryId, staffPrimary, true)),
            SafeRun(() => AssignAsync(deviceSecondaryId, staffSecondary, false)));

        results.Should().AllSatisfy(r => r.Should().BeTrue(
            "Secondary assignment phải luôn được chấp nhận cùng lúc với 1 Primary mới cho ĐÚNG điểm dừng đó - không bị khoá của Primary chặn/từ chối nhầm"));

        await using var check = Context();
        (await check.ScannerAssignments.Where(a => a.TripStopCallId == stopCallId && !a.IsDeleted).CountAsync()).Should().Be(2);
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
