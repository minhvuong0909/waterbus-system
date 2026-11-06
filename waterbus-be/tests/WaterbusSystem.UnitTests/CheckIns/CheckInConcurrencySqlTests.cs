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
