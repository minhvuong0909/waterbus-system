using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.UnitTests.Domain;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;

namespace WaterbusSystem.UnitTests.CheckIns;

/// <summary>
/// Review (human, commit fc2fc74): "Kiểm tra nâng cấp trên DB đã có dữ liệu và đã áp dụng migration
/// của PR #59 (EmailOutbox), không chỉ DB tạo mới". Các test SQL khác trong CheckInConcurrencySqlTests
/// luôn MigrateAsync() TOÀN BỘ chain ngay từ đầu (DB rỗng) - không mô phỏng đúng tình huống "DB
/// production đã có dữ liệu ScannerAssignment thật TRƯỚC khi cả 2 migration của PR #58 VÀ
/// migration AddEmailOutbox của PR #59 được áp dụng".
///
/// QUAN TRỌNG - phát hiện khi viết test này: migration "AddScannerAssignmentOpenEndedPrimaryUniqueIndex"
/// (20261106031430) có timestamp SỚM HƠN "AddEmailOutbox" (20261106042025) - vì migration đó được
/// tạo ra ở vòng review đầu tiên của PR #58, TRƯỚC khi nhánh này merge main (đã có PR #59). EF Core
/// migrator áp migration theo đúng THỨ TỰ TIMESTAMP trong assembly, không theo thứ tự merge Git -
/// nên "migrate tới đúng AddEmailOutbox" bằng assembly của nhánh NÀY vẫn áp luôn
/// AddScannerAssignmentOpenEndedPrimaryUniqueIndex trước đó (test ban đầu seed dữ liệu trùng sau mốc
/// đó bị chặn ngay ở bước seed, chưa tới bước migrate cần test - xem CI run thất bại ở PR #58 review
/// vòng 4). Mốc baseline THẬT phải là migration NGAY TRƯỚC CHK-03 trong chain
/// ("AddBoatPositionEventIndex") - đại diện đúng trạng thái DB trước khi CẢ PR #58 VÀ PR #59 tồn
/// tại, rồi áp tiếp TOÀN BỘ 3 migration còn lại (CHK-03 index, AddEmailOutbox, Restore index) theo
/// đúng thứ tự thời gian thật - đây chính xác là thứ tự sẽ xảy ra khi deploy, bất kể thứ tự merge Git.
/// </summary>
public sealed class ScannerAssignmentMigrationSqlTests : IAsyncLifetime
{
    private const string LastMigrationBeforeBothPRs = "20261012003336_AddBoatPositionEventIndex";
    private string? _connection;
    private ApplicationDbContext Context() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(_connection!).Options);

    public Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable("WATERBUS_TEST_SQLSERVER");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            _connection = new SqlConnectionStringBuilder(configured)
            { InitialCatalog = "WaterbusDomainTests_ScannerMigration_" + Guid.NewGuid().ToString("N") }.ConnectionString;
        }
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_connection is null) return;
        if (!new SqlConnectionStringBuilder(_connection).InitialCatalog.StartsWith("WaterbusDomainTests_ScannerMigration_"))
            throw new InvalidOperationException("Unsafe test database.");
        await using var c = Context();
        await c.Database.EnsureDeletedAsync();
    }

    private static async Task<List<string>> IndexNamesOnScannerAssignmentsAsync(ApplicationDbContext context)
    {
        // Không await using/Dispose connection ở đây - nó thuộc về context truyền vào (EF Core tự
        // quản lý vòng đời), chỉ mở/đóng tạm nếu context chưa tự mở sẵn.
        var conn = (SqlConnection)context.Database.GetDbConnection();
        var alreadyOpen = conn.State == System.Data.ConnectionState.Open;
        if (!alreadyOpen) await conn.OpenAsync();
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT name FROM sys.indexes WHERE object_id = OBJECT_ID('ScannerAssignments') AND name IS NOT NULL";
            var names = new List<string>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) names.Add(reader.GetString(0));
            return names;
        }
        finally
        {
            if (!alreadyOpen) await conn.CloseAsync();
        }
    }

    private static async Task<bool> TableExistsAsync(ApplicationDbContext context, string tableName)
    {
        var conn = (SqlConnection)context.Database.GetDbConnection();
        var alreadyOpen = conn.State == System.Data.ConnectionState.Open;
        if (!alreadyOpen) await conn.OpenAsync();
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM sys.tables WHERE name = @name";
            cmd.Parameters.Add(new SqlParameter("@name", tableName));
            return (int)(await cmd.ExecuteScalarAsync())! > 0;
        }
        finally
        {
            if (!alreadyOpen) await conn.CloseAsync();
        }
    }

    [SqlServerDomainFact]
    public async Task UpgradingFromBeforeBothPRs_WithExistingValidData_ShouldPreserveDataAndAddEverything()
    {
        // Mốc THẬT trước khi cả PR #58 và PR #59 tồn tại.
        await using (var c = Context())
        {
            await c.GetService<IMigrator>().MigrateAsync(LastMigrationBeforeBothPRs);
        }

        Guid stopCallId, assignmentId;
        await using (var seed = Context())
        {
            var scenario = CheckInScenarioFactory.SeedCheckInScenario(seed, Guid.NewGuid(), withAssignment: false);
            stopCallId = scenario.BoardingCall.Id;
            var device = new ScannerDevice { DeviceCode = "DEV-PRE-MIGRATE", IsActive = true };
            seed.ScannerDevices.Add(device);
            await seed.SaveChangesAsync();

            var assignment = new ScannerAssignment
            {
                DeviceId = device.Id, StaffAccountId = Guid.NewGuid(), TripStopCallId = stopCallId,
                IsPrimaryOffline = true, ActiveFrom = scenario.Now.AddHours(-1), ActiveUntil = null
            };
            seed.ScannerAssignments.Add(assignment);
            await seed.SaveChangesAsync();
            assignmentId = assignment.Id;
        }

        // Nâng cấp tiếp lên migration mới nhất (2 migration ScannerAssignment của PR #58).
        await using (var c = Context())
        {
            await c.Database.MigrateAsync();
            (await c.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
        }

        await using var check = Context();
        (await check.ScannerAssignments.FindAsync(assignmentId)).Should().NotBeNull(
            "dữ liệu ScannerAssignment đã tồn tại TRƯỚC 2 migration mới phải còn nguyên sau khi nâng cấp");

        (await TableExistsAsync(check, "EmailOutbox")).Should().BeTrue(
            "bảng EmailOutbox của PR #59 phải còn nguyên sau khi nâng cấp tiếp lên các migration mới của PR #58");

        var indexNames = await IndexNamesOnScannerAssignmentsAsync(check);
        indexNames.Should().Contain("IX_ScannerAssignments_TripStopCallId",
            "index chung (mọi bản ghi) phải tồn tại sau khi nâng cấp");
        indexNames.Should().Contain("IX_ScannerAssignments_TripStopCallId_OpenEndedPrimary",
            "index filtered unique (CHK-03) phải tồn tại sau khi nâng cấp");
    }

    [SqlServerDomainFact]
    public async Task UpgradingWithPreExistingDuplicateOpenEndedPrimaries_ShouldFailLoudlyNotSilentlyCorrupt()
    {
        // Review (human, commit fc2fc74): "xác nhận dữ liệu cũ có vi phạm unique index mới không" -
        // mô phỏng ĐÚNG tình huống xấu nhất: DB cũ (trước PR #58, chưa có index này) do CHÍNH race
        // đang được sửa (CHK-03) đã lỡ để lại 2 Primary Offline Scanner vô thời hạn cho CÙNG 1 điểm
        // dừng. Áp migration tạo unique index PHẢI thất bại rõ ràng (SQL Server từ chối tạo unique
        // index khi dữ liệu đang trùng) - KHÔNG được âm thầm xoá/sửa 1 trong 2 bản ghi lịch sử để
        // migration "qua được" (BR v2.0: không tự ý sửa dữ liệu nghiệp vụ đã có mà không qua đối
        // soát rõ ràng). Đây là hành vi ĐÚNG cần xác nhận, không phải lỗi cần sửa - nếu kịch bản này
        // từng xảy ra thật ngoài production, cần 1 bước dọn dữ liệu thủ công có kiểm soát TRƯỚC khi
        // chạy migration, ngoài phạm vi 1 migration tự động. PHẢI seed dữ liệu trùng ở mốc TRƯỚC
        // CHK-03 (LastMigrationBeforeBothPRs) - seed sau mốc AddEmailOutbox sẽ bị chặn ngay ở bước
        // seed (CHK-03 đã áp dụng do sớm hơn AddEmailOutbox theo timestamp), không test được đúng
        // điều cần test (migration tạo index mới là điểm thất bại, không phải bước seed).
        await using (var c = Context())
        {
            await c.GetService<IMigrator>().MigrateAsync(LastMigrationBeforeBothPRs);
        }

        Guid stopCallId;
        await using (var seed = Context())
        {
            var scenario = CheckInScenarioFactory.SeedCheckInScenario(seed, Guid.NewGuid(), withAssignment: false);
            stopCallId = scenario.BoardingCall.Id;

            var deviceA = new ScannerDevice { DeviceCode = "DEV-DUP-A", IsActive = true };
            var deviceB = new ScannerDevice { DeviceCode = "DEV-DUP-B", IsActive = true };
            seed.ScannerDevices.AddRange(deviceA, deviceB);
            await seed.SaveChangesAsync();

            seed.ScannerAssignments.AddRange(
                new ScannerAssignment
                {
                    DeviceId = deviceA.Id, StaffAccountId = Guid.NewGuid(), TripStopCallId = stopCallId,
                    IsPrimaryOffline = true, ActiveFrom = scenario.Now.AddHours(-2), ActiveUntil = null
                },
                new ScannerAssignment
                {
                    DeviceId = deviceB.Id, StaffAccountId = Guid.NewGuid(), TripStopCallId = stopCallId,
                    IsPrimaryOffline = true, ActiveFrom = scenario.Now.AddHours(-1), ActiveUntil = null
                });
            await seed.SaveChangesAsync();
        }

        await using var c2 = Context();
        var act = () => c2.Database.MigrateAsync();

        await act.Should().ThrowAsync<Exception>(
            "migration tạo unique filtered index PHẢI thất bại rõ ràng khi dữ liệu cũ đã vi phạm - " +
            "không được âm thầm xoá/sửa bản ghi lịch sử để migration \"qua được\"");

        await using var check = Context();
        var remaining = await check.ScannerAssignments.Where(a => a.TripStopCallId == stopCallId).CountAsync();
        remaining.Should().Be(2, "migration lỗi phải để DB ở trạng thái TRƯỚC migration - không được sót lại trạng thái nửa vời (mất 1 trong 2 bản ghi)");
    }
}
