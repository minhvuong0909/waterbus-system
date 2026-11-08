using System.Reflection;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Requests.CheckIns;
using WaterbusSystem.Application.Features.CheckIns;
using WaterbusSystem.Application.Features.CheckIns.Services;
using WaterbusSystem.Application.Features.ScannerDevices.Services;
using WaterbusSystem.Application.Interfaces.Persistence;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.Infrastructure.Repositories;
using WaterbusSystem.Infrastructure.Services.Checkout;
using WaterbusSystem.UnitTests.Domain;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;

namespace WaterbusSystem.UnitTests.CheckIns;

/// <summary>
/// Review PR #63: AssignScanner (check IsActive rồi insert) và Deactivate (đếm phân công rồi IsActive=false) từng có thể xen nhau,
/// để lại phân công trên thiết bị đã ngừng. Test dùng SQL Server thật và đồng bộ ĐÚNG tại biên check/write: bên thứ nhất bị giữ
/// SAU khi đã kiểm tra, TRƯỚC khi ghi (decorator ở tầng test, không đổi code production); sau đó chờ tới khi bên thứ hai THẬT SỰ
/// nằm trong hàng đợi sp_getapplock (sys.dm_tran_locks) rồi mới thả bên thứ nhất - không dùng Task.Delay để "hy vọng" xen nhau.
/// </summary>
public sealed class ScannerDeviceLockSqlTests : IAsyncLifetime
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
    private string? _connection;
    private ApplicationDbContext Context() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(_connection!).Options);

    public async Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable("WATERBUS_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(configured)) return;
        _connection = new SqlConnectionStringBuilder(configured) { InitialCatalog = "WaterbusDomainTests_DevLock_" + Guid.NewGuid().ToString("N") }.ConnectionString;
        await using var c = Context();
        await c.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_connection is null) return;
        if (!new SqlConnectionStringBuilder(_connection).InitialCatalog.StartsWith("WaterbusDomainTests_DevLock_"))
            throw new InvalidOperationException("Unsafe test database.");
        await using var c = Context();
        await c.Database.EnsureDeletedAsync();
    }

    // ---------- Hạ tầng test ----------

    /// <summary>Chạy hook SAU KHI method (theo tên) của repository thật hoàn tất - tức đúng biên "đã kiểm tra, chưa ghi".</summary>
    private class HookProxy : DispatchProxy
    {
        private object _inner = null!;
        private string _method = "";
        private Func<Task> _after = null!;

        public static T Create<T>(T inner, string method, Func<Task> after) where T : class
        {
            var proxy = Create<T, HookProxy>();
            var p = (HookProxy)(object)proxy;
            p._inner = inner; p._method = method; p._after = after;
            return proxy;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            var result = targetMethod!.Invoke(_inner, args);
            if (targetMethod.Name != _method || result is not Task task) return result;
            if (!targetMethod.ReturnType.IsGenericType) return Await(task);
            var generic = typeof(HookProxy).GetMethod(nameof(AwaitOf), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(targetMethod.ReturnType.GetGenericArguments()[0]);
            return generic.Invoke(this, [result]);
        }

        private async Task Await(Task t) { await t; await _after(); }
        private async Task<T> AwaitOf<T>(Task<T> t) { var r = await t; await _after(); return r; }
    }

    /// <summary>Cặp rào: bên bị giữ báo "đã tới biên" rồi chờ được thả.</summary>
    private sealed class Pause
    {
        private readonly TaskCompletionSource _reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task Reached => _reached.Task.WaitAsync(Timeout);
        public void Release() => _release.TrySetResult();
        public Func<Task> Hook => async () => { _reached.TrySetResult(); await _release.Task.WaitAsync(Timeout); };
    }

    /// <summary>Bọc khoá thật nhưng rút ngắn thời gian chờ để test timeout không phải chờ 10 giây.</summary>
    private sealed class ShortWaitLock(IScannerAssignmentLock inner) : IScannerAssignmentLock
    {
        public Task<IAsyncDisposable?> TryAcquireAsync(Guid tripStopCallId, TimeSpan wait, CancellationToken ct = default) =>
            inner.TryAcquireAsync(tripStopCallId, TimeSpan.FromMilliseconds(300), ct);
        public Task<IAsyncDisposable?> TryAcquireDeviceAsync(Guid deviceId, TimeSpan wait, CancellationToken ct = default) =>
            inner.TryAcquireDeviceAsync(deviceId, TimeSpan.FromMilliseconds(300), ct);
    }

    private sealed record Seeded(Guid StopCallId, Guid DeviceA, Guid DeviceB, DateTimeOffset Now);

    private async Task<Seeded> SeedAsync()
    {
        await using var seed = Context();
        var scenario = CheckInScenarioFactory.SeedCheckInScenario(seed, Guid.NewGuid(), withAssignment: false);
        var a = new ScannerDevice { DeviceCode = "LOCK-A", IsActive = true };
        var b = new ScannerDevice { DeviceCode = "LOCK-B", IsActive = true };
        seed.ScannerDevices.AddRange(a, b);
        await seed.SaveChangesAsync();
        return new Seeded(scenario.BoardingCall.Id, a.Id, b.Id, scenario.Now);
    }

    private static IDateTimeService Clock(DateTimeOffset now)
    {
        var clock = new Mock<IDateTimeService>();
        clock.Setup(x => x.Now).Returns(now);
        return clock.Object;
    }

    private static CheckInService AssignService(ApplicationDbContext c, DateTimeOffset now, Func<Task>? afterDeviceRead = null, bool shortWait = false)
    {
        var user = new Mock<ICurrentUserService>();
        user.Setup(x => x.UserId).Returns(Guid.NewGuid().ToString());
        user.Setup(x => x.UserRole).Returns("Admin");
        ICheckInRepository repo = new CheckInRepository(c);
        if (afterDeviceRead is not null) repo = HookProxy.Create(repo, nameof(ICheckInRepository.GetRequiredDeviceAsync), afterDeviceRead);
        IScannerAssignmentLock gate = new SqlScannerAssignmentLock(c);
        if (shortWait) gate = new ShortWaitLock(gate);
        return new CheckInService(repo, new EfUnitOfWork(c), user.Object, Clock(now), new CheckInProcessor(repo), gate,
            Options.Create(new NoShowFinalizationOptions()), NullLogger<CheckInService>.Instance);
    }

    private static ScannerDeviceAdminService DeactivateService(ApplicationDbContext c, DateTimeOffset now, Func<Task>? afterCount = null, bool shortWait = false)
    {
        IScannerDeviceAdminRepository repo = new ScannerDeviceAdminRepository(c);
        if (afterCount is not null) repo = HookProxy.Create(repo, nameof(IScannerDeviceAdminRepository.CountActiveAssignmentsAsync), afterCount);
        IScannerAssignmentLock gate = new SqlScannerAssignmentLock(c);
        if (shortWait) gate = new ShortWaitLock(gate);
        return new ScannerDeviceAdminService(repo, new EfUnitOfWork(c), new SqlCommercialTransactionRunner(c), Clock(now), gate);
    }

    private static AssignScannerRequest Request(Seeded s, Guid deviceId, bool primary) =>
        new(deviceId, Guid.NewGuid(), s.StopCallId, primary, s.Now.AddHours(-1), null);

    /// <summary>Chờ tới khi có ĐÚNG ít nhất 1 session đang xếp hàng chờ 1 applock trong DB test này.</summary>
    private async Task WaitForApplockWaiterAsync()
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (DateTime.UtcNow < deadline)
        {
            await using var c = Context();
            var waiting = await c.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS Value FROM sys.dm_tran_locks WHERE resource_type = 'APPLICATION' AND request_status = 'WAIT' AND resource_database_id = DB_ID()")
                .SingleAsync();
            if (waiting > 0) return;
            await Task.Delay(25);
        }
        throw new TimeoutException("Không có session nào xếp hàng chờ applock.");
    }

    private async Task<(int Assignments, bool DeviceActive)> StateAsync(Guid deviceId)
    {
        await using var c = Context();
        return (await c.ScannerAssignments.CountAsync(a => a.DeviceId == deviceId),
            (await c.ScannerDevices.SingleAsync(d => d.Id == deviceId)).IsActive);
    }

    // ---------- Assignment trước, Deactivate xen vào ----------

    private async Task AssignThenDeactivate(bool primary)
    {
        var s = await SeedAsync();
        var pause = new Pause();

        var assign = Task.Run(async () =>
        {
            await using var c = Context();
            await AssignService(c, s.Now, pause.Hook).AssignScannerAsync(Request(s, s.DeviceA, primary));
        });
        await pause.Reached;                       // Assign đã thấy device Active, chưa insert

        var deactivate = Task.Run(async () =>
        {
            await using var c = Context();
            await DeactivateService(c, s.Now).DeactivateAsync(s.DeviceA);
        });
        await WaitForApplockWaiterAsync();         // Deactivate THẬT SỰ đang chờ khoá DeviceId
        deactivate.IsCompleted.Should().BeFalse("Deactivate phải bị chặn bởi khoá DeviceId đang do Assign giữ");

        pause.Release();
        await assign;                              // Assign thành công
        var act = async () => await deactivate;    // Deactivate làm lại kiểm tra SAU khoá -> thấy phân công vừa commit
        await act.Should().ThrowAsync<ConflictException>().WithMessage("*phân công chưa kết thúc*");

        var (assignments, active) = await StateAsync(s.DeviceA);
        assignments.Should().Be(1);
        active.Should().BeTrue("thiết bị phải còn Active vì Deactivate bị từ chối");
    }

    [SqlServerDomainFact]
    public Task PrimaryAssignmentFirst_ThenDeactivate_ShouldRejectDeactivateAndKeepAssignment() => AssignThenDeactivate(primary: true);

    [SqlServerDomainFact]
    public Task SecondaryAssignmentFirst_ThenDeactivate_ShouldRejectDeactivateAndKeepAssignment() => AssignThenDeactivate(primary: false);

    // ---------- Deactivate trước, Assignment xen vào ----------

    private async Task DeactivateThenAssign(bool primary)
    {
        var s = await SeedAsync();
        var pause = new Pause();

        var deactivate = Task.Run(async () =>
        {
            await using var c = Context();
            await DeactivateService(c, s.Now, pause.Hook).DeactivateAsync(s.DeviceA);
        });
        await pause.Reached;                       // Deactivate đã đếm "0 phân công", chưa ghi IsActive=false

        var assign = Task.Run(async () =>
        {
            await using var c = Context();
            await AssignService(c, s.Now).AssignScannerAsync(Request(s, s.DeviceA, primary));
        });
        await WaitForApplockWaiterAsync();
        assign.IsCompleted.Should().BeFalse("Assign phải bị chặn bởi khoá DeviceId đang do Deactivate giữ");

        pause.Release();
        await deactivate;
        var act = async () => await assign;        // Assign đọc lại SAU khoá -> thấy IsActive=false
        await act.Should().ThrowAsync<ConflictException>().WithMessage("*ngưng hoạt động*");

        var (assignments, active) = await StateAsync(s.DeviceA);
        assignments.Should().Be(0, "không được tồn tại phân công thành công nào trên thiết bị đã ngừng");
        active.Should().BeFalse();
    }

    [SqlServerDomainFact]
    public Task DeactivateFirst_ThenPrimaryAssignment_ShouldRejectAssignment() => DeactivateThenAssign(primary: true);

    [SqlServerDomainFact]
    public Task DeactivateFirst_ThenSecondaryAssignment_ShouldRejectAssignment() => DeactivateThenAssign(primary: false);

    // ---------- Thiết bị khác không bị chặn ----------

    [SqlServerDomainFact]
    public async Task OperationsOnDifferentDevices_ShouldNotBlockEachOther()
    {
        var s = await SeedAsync();
        var pause = new Pause();

        var heldAssign = Task.Run(async () =>
        {
            await using var c = Context();
            await AssignService(c, s.Now, pause.Hook).AssignScannerAsync(Request(s, s.DeviceA, primary: false));
        });
        await pause.Reached;                       // khoá DeviceA đang bị giữ

        // Deactivate + Assign Secondary trên DeviceB phải hoàn tất khi DeviceA vẫn bị giữ (không có khoá toàn cục).
        await using (var c = Context()) await DeactivateService(c, s.Now).DeactivateAsync(s.DeviceB).WaitAsync(Timeout);
        (await StateAsync(s.DeviceB)).DeviceActive.Should().BeFalse();
        heldAssign.IsCompleted.Should().BeFalse("bên giữ khoá DeviceA vẫn chưa được thả");

        pause.Release();
        await heldAssign;
        (await StateAsync(s.DeviceA)).Should().Be((1, true));
    }

    // ---------- Primary overlap (#58) vẫn đúng khi có khoá thiết bị ----------

    [SqlServerDomainFact]
    public async Task PrimaryOverlapRule_StillRejectsSecondDeviceOnSameStop_WithDeviceLockInPlace()
    {
        var s = await SeedAsync();
        await using (var c = Context()) await AssignService(c, s.Now).AssignScannerAsync(Request(s, s.DeviceA, primary: true));

        var act = async () =>
        {
            await using var c = Context();
            await AssignService(c, s.Now).AssignScannerAsync(Request(s, s.DeviceB, primary: true));
        };
        await act.Should().ThrowAsync<ConflictException>().WithMessage("*Primary Offline Scanner khác*");
        (await StateAsync(s.DeviceB)).Assignments.Should().Be(0);
    }

    // ---------- Timeout / lỗi: kết quả có thể đoán trước, không ghi dở, không rò khoá ----------

    private async Task<SqlConnection> HoldDeviceLockAsync(Guid deviceId)
    {
        var conn = new SqlConnection(_connection);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DECLARE @r int; EXEC @r = sp_getapplock @Resource = @res, @LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = 0; SELECT @r;";
        cmd.Parameters.AddWithValue("@res", "waterbus:scanner-device:" + deviceId.ToString("N"));
        ((int)(await cmd.ExecuteScalarAsync())!).Should().BeGreaterThanOrEqualTo(0);
        return conn;
    }

    [SqlServerDomainFact]
    public async Task LockTimeout_ShouldReturnConflictWithoutPartialWrites_AndNotLeakLock()
    {
        var s = await SeedAsync();
        var holder = await HoldDeviceLockAsync(s.DeviceA);
        try
        {
            var assign = async () =>
            {
                await using var c = Context();
                await AssignService(c, s.Now, shortWait: true).AssignScannerAsync(Request(s, s.DeviceA, primary: true));
            };
            await assign.Should().ThrowAsync<ConflictException>().WithMessage("*đang được xử lý*");

            var deactivate = async () =>
            {
                await using var c = Context();
                await DeactivateService(c, s.Now, shortWait: true).DeactivateAsync(s.DeviceA);
            };
            await deactivate.Should().ThrowAsync<ConflictException>().WithMessage("*đang được xử lý*");

            (await StateAsync(s.DeviceA)).Should().Be((0, true), "timeout không được để lại phân công hay đổi trạng thái thiết bị");
        }
        finally
        {
            await holder.DisposeAsync();
        }

        // Khoá không bị rò sau các lần timeout: giờ thao tác bình thường phải chạy được ngay.
        await using (var c = Context()) await AssignService(c, s.Now, shortWait: true).AssignScannerAsync(Request(s, s.DeviceA, primary: false));
        (await StateAsync(s.DeviceA)).Should().Be((1, true));
    }

    [SqlServerDomainFact]
    public async Task FailedAssignmentOnInactiveDevice_ShouldReleaseLockForNextCaller()
    {
        var s = await SeedAsync();
        await using (var c = Context()) await DeactivateService(c, s.Now).DeactivateAsync(s.DeviceA);

        // Lần 1 lỗi nghiệp vụ (đã giữ khoá rồi mới throw) - lần 2 với thời gian chờ 300ms vẫn phải thấy lỗi "ngưng hoạt động",
        // KHÔNG phải lỗi timeout khoá => khoá đã được release ở nhánh lỗi.
        var alive = new List<ApplicationDbContext>();   // context KHÔNG dispose: dispose connection sẽ che lỗi rò khoá
        try
        {
            for (var i = 0; i < 2; i++)
            {
                var c = Context();
                alive.Add(c);
                var act = async () => await AssignService(c, s.Now, shortWait: true).AssignScannerAsync(Request(s, s.DeviceA, primary: i == 0));
                await act.Should().ThrowAsync<ConflictException>().WithMessage("*ngưng hoạt động*");

                // Session SQL độc lập: không còn applock nào đang được cấp dù context lỗi vẫn sống.
                await using var probe = Context();
                (await probe.Database.SqlQueryRaw<int>(
                    "SELECT COUNT(*) AS Value FROM sys.dm_tran_locks WHERE resource_type = 'APPLICATION' AND request_status = 'GRANT' AND resource_database_id = DB_ID()")
                    .SingleAsync()).Should().Be(0, "khoá phải được release ở nhánh lỗi, không đợi dispose context");
            }
        }
        finally
        {
            foreach (var c in alive) await c.DisposeAsync();
        }
        (await StateAsync(s.DeviceA)).Should().Be((0, false));
    }
}
