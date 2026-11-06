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
/// Review (human, commit fc2fc74, rồi vòng 5 trên commit 3941cd5/fc2fc74): "Kiểm tra nâng cấp trên
/// DB đã có dữ liệu và đã áp dụng migration của PR #59 (EmailOutbox), không chỉ DB tạo mới".
///
/// PHÁT HIỆN quan trọng khi viết test này: migration "AddScannerAssignmentOpenEndedPrimaryUniqueIndex"
/// (20261106031430) có timestamp SỚM HƠN "AddEmailOutbox" (20261106042025) - vì migration đó được
/// tạo ra ở vòng review đầu tiên của PR #58, TRƯỚC khi nhánh này merge main (đã có PR #59). EF Core
/// migrator áp migration theo đúng THỨ TỰ TIMESTAMP trong assembly, không theo thứ tự merge Git - nên
/// "migrate tới đúng AddEmailOutbox" bằng assembly của nhánh này vẫn áp luôn
/// AddScannerAssignmentOpenEndedPrimaryUniqueIndex trước đó. Mốc baseline ĐÚNG phải là migration
/// NGAY TRƯỚC CHK-03 trong chain ("AddBoatPositionEventIndex") - đại diện đúng trạng thái DB trước
/// khi CẢ PR #58 VÀ PR #59 tồn tại (migration này y hệt trên cả nhánh này và origin/main, không bị
/// sửa đổi - AGENTS.md: "KHÔNG sửa migration đã merge" - nên dùng chung 1 assembly để migrate tới
/// mốc này tương đương với dùng đúng schema thật của origin/main tại điểm đó).
///
/// PendingMigrationsAfterBaseline_MustBeExactlyThisPrsNewMigrations_InOrder tự kiểm chứng đúng giả
/// định này ở mỗi lần CI chạy - nếu ai thêm 1 migration mới vào main với timestamp nằm giữa baseline
/// và latest mà quên cập nhật hằng số LastMigrationBeforeBothPRs, test đó sẽ FAIL ngay (không để 2
/// test nâng cấp dưới âm thầm test sai mốc như đã từng xảy ra ở vòng review trước).
/// </summary>
public sealed class ScannerAssignmentMigrationSqlTests : IAsyncLifetime
{
    private const string LastMigrationBeforeBothPRs = "20261012003336_AddBoatPositionEventIndex";

    // Đúng 3 migration MỚI của 2 PR, theo ĐÚNG thứ tự timestamp thật sẽ được áp dụng khi deploy -
    // CHK-03 (PR #58, nhưng timestamp sớm hơn) -> EmailOutbox (PR #59) -> Restore index (PR #58).
    private static readonly string[] ExpectedNewMigrationsInOrder =
    [
        "20261106031430_AddScannerAssignmentOpenEndedPrimaryUniqueIndex",
        "20261106042025_AddEmailOutbox",
        "20261106090321_RestoreScannerAssignmentTripStopCallIdIndex",
    ];

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

    private sealed record IndexInfo(string Name, bool IsUnique, string? FilterDefinition);

    private static async Task<List<IndexInfo>> IndexesOnScannerAssignmentsAsync(ApplicationDbContext context)
    {
        // Không await using/Dispose connection ở đây - nó thuộc về context truyền vào (EF Core tự
        // quản lý vòng đời), chỉ mở/đóng tạm nếu context chưa tự mở sẵn.
        var conn = (SqlConnection)context.Database.GetDbConnection();
        var alreadyOpen = conn.State == System.Data.ConnectionState.Open;
        if (!alreadyOpen) await conn.OpenAsync();
        try
        {
            await using var cmd = conn.CreateCommand();
            // is_unique và filter_definition đọc trực tiếp từ sys.indexes - xác nhận ĐÚNG cấu hình
            // (không chỉ tồn tại bằng tên) theo đúng yêu cầu review.
            cmd.CommandText = "SELECT name, is_unique, filter_definition FROM sys.indexes " +
                               "WHERE object_id = OBJECT_ID('ScannerAssignments') AND name IS NOT NULL";
            var results = new List<IndexInfo>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new IndexInfo(
                    reader.GetString(0),
                    reader.GetBoolean(1),
                    reader.IsDBNull(2) ? null : reader.GetString(2)));
            }
            return results;
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
    public async Task PendingMigrationsAfterBaseline_MustBeExactlyThisPrsNewMigrations_InOrder()
    {
        // Tự-kiểm-chứng mốc baseline (review vòng 5 - "sửa test setup để khởi tạo database từ
        // schema/migration thực tế của origin/main"): nếu danh sách pending sau baseline khác với
        // ĐÚNG 3 migration mới của 2 PR theo ĐÚNG thứ tự, 2 test nâng cấp dưới đang test SAI tiền
        // đề - test này phải FAIL trước để lộ ra, không để chúng âm thầm pass/fail sai lý do.
        await using var c = Context();
        await c.GetService<IMigrator>().MigrateAsync(LastMigrationBeforeBothPRs);
        var pending = (await c.Database.GetPendingMigrationsAsync()).ToList();
        pending.Should().Equal(ExpectedNewMigrationsInOrder,
            "LastMigrationBeforeBothPRs phải là migration NGAY TRƯỚC 3 migration mới của PR #58/#59, theo đúng thứ tự áp dụng thật");
    }

    [SqlServerDomainFact]
    public async Task UpgradingFromBeforeBothPRs_WithExistingValidData_ShouldPreserveDataAndConfigureBothIndexesCorrectly()
    {
        // Mốc THẬT trước khi cả PR #58 và PR #59 tồn tại (xem PendingMigrationsAfterBaseline... ở trên).
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

        // Nâng cấp tiếp lên migration mới nhất (3 migration mới của PR #58/#59).
        await using (var c = Context())
        {
            await c.Database.MigrateAsync();
            (await c.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
        }

        await using var check = Context();
        (await check.ScannerAssignments.FindAsync(assignmentId)).Should().NotBeNull(
            "dữ liệu ScannerAssignment đã tồn tại TRƯỚC 3 migration mới phải còn nguyên sau khi nâng cấp");

        (await TableExistsAsync(check, "EmailOutbox")).Should().BeTrue(
            "bảng EmailOutbox của PR #59 phải còn nguyên sau khi nâng cấp tiếp lên các migration mới của PR #58");

        var indexes = await IndexesOnScannerAssignmentsAsync(check);

        var generalIndex = indexes.SingleOrDefault(i => i.Name == "IX_ScannerAssignments_TripStopCallId");
        generalIndex.Should().NotBeNull("index chung (mọi bản ghi) phải tồn tại sau khi nâng cấp");
        generalIndex!.IsUnique.Should().BeFalse("index chung KHÔNG được là unique - nó phải bao mọi bản ghi, kể cả nhiều Secondary/hữu hạn trùng điểm dừng");
        generalIndex.FilterDefinition.Should().BeNull("index chung không được có filter - phải phủ TOÀN BỘ bản ghi");

        var filteredIndex = indexes.SingleOrDefault(i => i.Name == "IX_ScannerAssignments_TripStopCallId_OpenEndedPrimary");
        filteredIndex.Should().NotBeNull("index filtered unique (CHK-03) phải tồn tại sau khi nâng cấp");
        filteredIndex!.IsUnique.Should().BeTrue("index CHK-03 phải là UNIQUE - đây chính là ràng buộc chặn race Primary vô thời hạn");
        filteredIndex.FilterDefinition.Should().NotBeNullOrEmpty("index CHK-03 phải có filter - chỉ áp dụng cho Primary vô thời hạn, chưa xoá");
        filteredIndex.FilterDefinition.Should().Contain("IsPrimaryOffline").And.Contain("ActiveUntil").And.Contain("IsDeleted");
    }

    [SqlServerDomainFact]
    public async Task UpgradingWithPreExistingDuplicateOpenEndedPrimaries_ShouldFailWithUniqueKeyViolationNotSilentlyCorrupt()
    {
        // Review (human): "xác nhận dữ liệu cũ có vi phạm unique index mới không" - mô phỏng ĐÚNG
        // tình huống xấu nhất: DB cũ (trước PR #58, chưa có index này) do CHÍNH race đang được sửa
        // (CHK-03) đã lỡ để lại 2 Primary Offline Scanner vô thời hạn cho CÙNG 1 điểm dừng. Áp
        // migration tạo unique index PHẢI thất bại rõ ràng - KHÔNG được âm thầm xoá/sửa 1 trong 2
        // bản ghi lịch sử để migration "qua được" (BR v2.0: không tự ý sửa dữ liệu nghiệp vụ đã có
        // mà không qua đối soát rõ ràng). Đây là hành vi ĐÚNG cần xác nhận, không phải lỗi cần sửa.
        //
        // Review vòng 5: "Không dùng ThrowAsync<Exception> quá rộng để che lỗi khác" - assert cụ thể
        // SqlException với đúng mã lỗi vi phạm unique constraint (2601/2627, giống hệt constants
        // trong EfUnitOfWork.cs), và xác nhận migration dừng lại ĐÚNG TẠI migration tạo index (không
        // phải lỗi ngẫu nhiên khác) bằng cách kiểm tra migration cuối cùng ÁP DỤNG THÀNH CÔNG vẫn là
        // baseline - CHK-03 (migration pending đầu tiên) không được coi là đã áp dụng.
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

        // SqlException cụ thể với mã lỗi 1505 ("CREATE UNIQUE INDEX terminated because a duplicate
        // key was found") - LƯU Ý: đây KHÁC với 2601/2627 (chỉ xảy ra khi INSERT/UPDATE vi phạm 1
        // unique index ĐÃ TỒN TẠI từ trước, như trong EfUnitOfWork.cs cho race CHK-02/CHK-06). Ở
        // đây tình huống ngược lại: chính câu lệnh DDL "CREATE UNIQUE INDEX" của migration phát
        // hiện dữ liệu ĐÃ CÓ SẴN bị trùng trước khi index kịp tồn tại - SQL Server báo lỗi 1505 cho
        // đúng trường hợp này (đã xác nhận bằng CI thật, không phải đoán). KHÔNG assert Exception
        // chung để tránh che giấu 1 lỗi hoàn toàn khác (vd lỗi cú pháp migration, mất kết nối) mà vẫn
        // làm test "pass" sai ý nghĩa. Tự dò qua InnerException (không giả định EF bọc hay không bọc
        // SqlException gốc khi 1 migration raw-SQL thất bại) để chắc chắn bắt đúng exception thật,
        // không phụ thuộc vào chi tiết implementation của EF Core migrator.
        SqlException? sqlError = null;
        try
        {
            await c2.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            for (var current = ex; current is not null; current = current.InnerException)
            {
                if (current is SqlException found) { sqlError = found; break; }
            }
        }

        sqlError.Should().NotBeNull(
            "migration tạo unique filtered index PHẢI thất bại ĐÚNG VÌ vi phạm unique constraint ở SQL Server (SqlException) - " +
            "không phải vì 1 lỗi không liên quan nào khác, và không được âm thầm xoá/sửa bản ghi lịch sử để \"qua được\"");
        sqlError!.Number.Should().Be(1505,
            "mã lỗi SQL Server phải đúng là 'CREATE UNIQUE INDEX terminated because a duplicate key was found' (1505), không phải 1 loại lỗi SQL khác");
        sqlError.Message.Should().Contain("IX_ScannerAssignments_TripStopCallId_OpenEndedPrimary",
            "lỗi phải chỉ rõ đúng tên index CHK-03 đang được tạo, xác nhận migration dừng lại tại ĐÚNG bước cần test");

        await using var check = Context();

        // Migration CHK-03 (pending đầu tiên) phải CHƯA được ghi nhận là đã áp dụng - xác nhận
        // migration dừng lại đúng ở lần thử ĐẦU TIÊN, không phải lỗi xảy ra ở 1 migration khác sau đó.
        var appliedMigrations = (await check.Database.GetAppliedMigrationsAsync()).ToList();
        appliedMigrations.Should().NotContain(ExpectedNewMigrationsInOrder[0],
            "migration CHK-03 phải CHƯA được áp dụng (rollback khi lỗi) - migration dừng đúng tại bước tạo unique index");
        appliedMigrations.Last().Should().Be(LastMigrationBeforeBothPRs,
            "migration cuối cùng thực sự áp dụng thành công phải vẫn là baseline - không migration nào khác lỡ chạy qua");

        var remaining = await check.ScannerAssignments.Where(a => a.TripStopCallId == stopCallId).CountAsync();
        remaining.Should().Be(2, "migration lỗi phải để DB ở trạng thái TRƯỚC migration - không được sót lại trạng thái nửa vời (mất 1 trong 2 bản ghi)");
    }
}
