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
/// Review (human, nhiều vòng): "Kiểm tra nâng cấp trên DB đã có dữ liệu và đã áp dụng migration của
/// PR #59 (EmailOutbox), không chỉ DB tạo mới" - rồi sau đó: "dựng database bằng migration assembly
/// CỦA CHÍNH main, rồi áp migration từ nhánh #58 lên trên, xác nhận dữ liệu/index giữ đúng".
///
/// HOTFIX (sau khi PR #58 đã merge vào main): 3 database baseline này TỪNG được CI dựng bằng
/// `dotnet ef database update` chạy trên code của ref DI ĐỘNG `origin/main`. Ngay khi PR #58 merge,
/// `origin/main` không còn đại diện cho "trước PR #58" nữa - baseline AfterEmailOutbox lúc đó đã
/// VÔ TÌNH chứa luôn cả 2 migration ScannerAssignment của #58, khiến
/// <see cref="UpgradingFromMainsFullLatest_WithExistingValidData_ShouldApplyBothOutOfChronologicalOrderMigrationsCorrectly"/>
/// fail trên CI của main (và lan ra làm đỏ Backend CI của các PR khác đang mở, vd #61). Bài học: một
/// ref di động không thể dùng để đại diện cho một điểm lịch sử cố định. Từ hotfix này, cả 3 baseline
/// đều dựng từ 1 SHA CỐ ĐỊNH, bất biến: `bb1bf0c` (trạng thái main ngay sau khi PR #59 - AddEmailOutbox
/// - merge, ngay TRƯỚC khi PR #58 merge) - SHA này không bao giờ đổi nghĩa dù main tiến lên bao nhiêu
/// commit nữa sau đó.
///
/// 3 database baseline được CI dựng THẬT bằng `dotnet ef database update` chạy TRÊN CODE CỦA SHA
/// `bb1bf0c` (qua git worktree, xem .github/workflows/ci.yml bước "Build migration baseline
/// databases from the pinned main commit's real code") - KHÔNG migrate bằng cách target 1 ID cũ
/// trong assembly của nhánh này (dù 2 cách cho cùng kết quả vì migration đã merge không bao giờ bị
/// sửa - AGENTS.md - nhưng reviewer muốn xác nhận bằng chính code thật của main, không chỉ bằng suy
/// luận tương đương):
/// - WaterbusDomainTests_MainBaselineValid/Duplicate: migrate tới ĐÚNG mốc trước cả 2 PR
///   (20261012003336_AddBoatPositionEventIndex) - mô phỏng "chưa chạy PR #58 lẫn #59".
/// - WaterbusDomainTests_MainBaselineAfterEmailOutbox: migrate tới ĐÍCH EXPLICIT 20261106042025_
///   AddEmailOutbox (migration cuối cùng thật sự tồn tại tại SHA bb1bf0c - KHÔNG bỏ trống target để
///   "migrate tới latest", vì ý nghĩa của "latest" phụ thuộc SHA đang checkout tại thời điểm chạy, dễ
///   âm thầm trỏ sang migration khác nếu SHA baseline từng bị đổi) - mô phỏng "main đã chạy PR #59
///   (EmailOutbox) nhưng CHƯA chạy PR #58", để xác nhận 2 migration ScannerAssignment của PR #58
///   (1 trong đó có timestamp SỚM HƠN AddEmailOutbox, dù áp dụng SAU nó trên môi trường này) vẫn áp
///   đúng, không mất dữ liệu.
/// 3 connection string được CI ghi vào $GITHUB_ENV, đọc qua WATERBUS_TEST_MAIN_BASELINE_VALID/
/// DUPLICATE/AFTER_EMAILOUTBOX ở đây.
///
/// KHÔNG tự load 1 bản WaterbusSystem.Infrastructure.dll khác của main vào CHÍNH process test này
/// (sẽ xung đột nạp 2 assembly cùng tên/cùng type identity mà EF Core dùng reflection để khớp model)
/// - build+migrate tại SHA bb1bf0c chạy ở 1 process `dotnet ef` HOÀN TOÀN riêng (bước CI), test chỉ
/// KẾT NỐI vào database đã có sẵn đó bằng ApplicationDbContext của CHÍNH nhánh này (an toàn, vì tại
/// thời điểm đó DB mới chỉ có schema chung - Trip/Booking/Ticket/ScannerAssignment... - tồn tại từ
/// trước cả 2 PR, đọc/viết bằng model nào cũng được miễn chưa chạm 2 migration mới).
///
/// Nếu chạy local không có 2 biến môi trường trên (vd không qua CI, hoặc WATERBUS_TEST_SQLSERVER có
/// nhưng chưa dựng baseline), 2 test cần nó sẽ Skip rõ ràng (MainBaselineFact) - không âm thầm pass.
/// PendingMigrationsAfterBaseline_... không cần baseline CI-dựng (chỉ tự kiểm chứng thứ tự migration
/// trong chính assembly đang test), nên vẫn chạy được với mọi SQL Server thật (SqlServerDomainFact).
/// </summary>
public sealed class ScannerAssignmentMigrationSqlTests
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

    /// <summary>Skip nếu thiếu SQL Server thật HOẶC thiếu database baseline do CI dựng sẵn từ SHA cố định bb1bf0c.</summary>
    private class MainBaselineFactAttribute : FactAttribute
    {
        public MainBaselineFactAttribute(string envVar)
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WATERBUS_TEST_SQLSERVER")))
            {
                Skip = "Set WATERBUS_TEST_SQLSERVER to a local SQL Server connection; tests use a separate temporary database.";
            }
            else if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(envVar)))
            {
                Skip = $"Set {envVar} to a database already migrated using the pinned main commit bb1bf0c's real code " +
                       "(CI builds this via git worktree + dotnet ef - see .github/workflows/ci.yml).";
            }
        }
    }

    private sealed class MainBaselineValidFactAttribute() : MainBaselineFactAttribute("WATERBUS_TEST_MAIN_BASELINE_VALID");
    private sealed class MainBaselineDuplicateFactAttribute() : MainBaselineFactAttribute("WATERBUS_TEST_MAIN_BASELINE_DUPLICATE");
    private sealed class MainBaselineAfterEmailOutboxFactAttribute() : MainBaselineFactAttribute("WATERBUS_TEST_MAIN_BASELINE_AFTER_EMAILOUTBOX");

    private static ApplicationDbContext Context(string connectionString) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connectionString).Options);

    /// <summary>
    /// Context chỉ dùng để SEED vào database baseline (schema của SHA cũ bb1bf0c, TRƯỚC các migration mới).
    /// Model hiện tại có cột mới trên bảng cũ mà baseline chưa có (vd FinancialTransactions.IsDemoRefund) nên INSERT bằng
    /// model đầy đủ sẽ lỗi "Invalid column name". Mỗi cột thêm vào bảng CÓ SẴN ở baseline phải được Ignore tại đây.
    /// </summary>
    private sealed class BaselineSeedContext(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.Entity<FinancialTransaction>().Ignore(x => x.IsDemoRefund);
            builder.Entity<FinancialTransaction>().Ignore(x => x.ExportedAt);
            builder.Entity<FinancialTransaction>().Ignore(x => x.ExportedByAdminId);
        }
    }

    private static ApplicationDbContext SeedContext(string connectionString) =>
        new BaselineSeedContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connectionString).Options);

    /// <summary>
    /// Xoá sạch database baseline sau khi test xong (thành công hay thất bại) để lần CI kế tiếp dựng
    /// lại từ đầu sạch sẽ - guard cùng pattern "chỉ xoá đúng tên test DB" dùng ở các lớp test khác.
    /// </summary>
    private static async Task CleanupBaselineDatabaseAsync(string connectionString)
    {
        var catalog = new SqlConnectionStringBuilder(connectionString).InitialCatalog;
        if (!catalog.StartsWith("WaterbusDomainTests_MainBaseline"))
            throw new InvalidOperationException("Unsafe baseline database name - refusing to drop.");
        await using var c = Context(connectionString);
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
        // Tự-kiểm-chứng mốc baseline: nếu danh sách pending sau baseline khác với ĐÚNG 3 migration
        // mới của 2 PR theo ĐÚNG thứ tự, 2 test nâng cấp dưới (dùng database do CI dựng sẵn ở ĐÚNG
        // mốc này) đang test SAI tiền đề - test này phải FAIL trước để lộ ra. Dùng 1 DB tạm riêng
        // (không phải 2 baseline cố định của CI) vì chỉ cần migrate-rồi-xem-pending, không cần dữ
        // liệu/schema do code thật của main dựng.
        var configured = Environment.GetEnvironmentVariable("WATERBUS_TEST_SQLSERVER")!;
        var tempConnection = new SqlConnectionStringBuilder(configured)
        { InitialCatalog = "WaterbusDomainTests_ScannerMigrationSelfCheck_" + Guid.NewGuid().ToString("N") }.ConnectionString;
        try
        {
            await using var c = Context(tempConnection);
            await c.GetService<IMigrator>().MigrateAsync(LastMigrationBeforeBothPRs);
            var pending = (await c.Database.GetPendingMigrationsAsync()).ToList();
            // Các migration thêm SAU 3 migration của PR #58/#59 (vd AddAudioPlaybackRequests) không làm sai tiền đề baseline.
            pending.Take(ExpectedNewMigrationsInOrder.Length).Should().Equal(ExpectedNewMigrationsInOrder,
                "LastMigrationBeforeBothPRs phải là migration NGAY TRƯỚC 3 migration mới của PR #58/#59, theo đúng thứ tự áp dụng thật");
        }
        finally
        {
            await using var c = Context(tempConnection);
            await c.Database.EnsureDeletedAsync();
        }
    }

    [MainBaselineValidFact]
    public async Task UpgradingFromBeforeBothPRs_WithExistingValidData_ShouldPreserveDataAndConfigureBothIndexesCorrectly()
    {
        // Database này đã được CI migrate tới ĐÚNG LastMigrationBeforeBothPRs bằng chính code của
        // SHA cố định bb1bf0c trên main (git worktree + dotnet ef - xem ci.yml) - KHÔNG phải
        // assembly của nhánh này.
        var connectionString = Environment.GetEnvironmentVariable("WATERBUS_TEST_MAIN_BASELINE_VALID")!;
        try
        {
            Guid stopCallId, assignmentId;
            await using (var seed = SeedContext(connectionString))
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

            // Nâng cấp tiếp lên migration mới nhất bằng assembly của NHÁNH NÀY (3 migration mới của
            // PR #58/#59) - DB nguồn đã là schema THẬT của SHA cố định bb1bf0c trên main, không phải giả định.
            await using (var c = Context(connectionString))
            {
                await c.Database.MigrateAsync();
                (await c.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
            }

            await using var check = Context(connectionString);
            (await check.ScannerAssignments.FindAsync(assignmentId)).Should().NotBeNull(
                "dữ liệu ScannerAssignment đã tồn tại TRƯỚC 3 migration mới (ghi bằng code thật của SHA bb1bf0c trên main) phải còn nguyên sau khi nâng cấp");

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
        finally
        {
            await CleanupBaselineDatabaseAsync(connectionString);
        }
    }

    [MainBaselineAfterEmailOutboxFact]
    public async Task UpgradingFromMainsFullLatest_WithExistingValidData_ShouldApplyBothOutOfChronologicalOrderMigrationsCorrectly()
    {
        // Review (human): không chỉ kiểm từ mốc TRƯỚC cả 2 PR - còn phải kiểm từ 1 DB đã chạy TOÀN
        // BỘ migration thật của main TẠI SHA CỐ ĐỊNH bb1bf0c (bao gồm AddEmailOutbox của PR #59,
        // migration cuối cùng thật sự tồn tại ở SHA đó), rồi mới áp 2 migration ScannerAssignment
        // của PR #58 lên trên. Đây là tình huống "ngược thời gian" thật sẽ xảy ra khi deploy:
        // AddScannerAssignmentOpenEndedPrimaryUniqueIndex (20261106031430) có ID/timestamp NHỎ HƠN
        // AddEmailOutbox (20261106042025) dù được ÁP DỤNG SAU nó trên môi trường này (vì
        // AddEmailOutbox đã nằm trong main từ trước, còn 2 migration ScannerAssignment chỉ mới
        // pending). EF Core áp các migration PENDING theo thứ tự ID của riêng chúng (không cần
        // "liền mạch" với các migration ĐÃ áp dụng từ trước) - test này xác nhận điều đó hoạt động
        // đúng trong thực tế, không chỉ về lý thuyết.
        //
        // HOTFIX: baseline này TỪNG được CI dựng bằng "migrate tới latest của origin/main" (ref di
        // động) - ngay khi PR #58 merge vào main, "latest của origin/main" tự động trở thành CHÍNH
        // 2 migration ScannerAssignment đang được test ở đây, làm pendingBeforeUpgrade rỗng và assert
        // dưới đây fail (CI đỏ, lan ra các PR khác). Nay baseline dựng từ SHA cố định bb1bf0c với
        // ĐÍCH EXPLICIT "20261106042025_AddEmailOutbox" (xem ci.yml) - SHA và đích đều bất biến, nên
        // pendingBeforeUpgrade dưới đây luôn đúng là đúng 2 migration ScannerAssignment, mãi mãi.
        var connectionString = Environment.GetEnvironmentVariable("WATERBUS_TEST_MAIN_BASELINE_AFTER_EMAILOUTBOX")!;
        try
        {
            Guid stopCallId, assignmentId;
            await using (var seed = SeedContext(connectionString))
            {
                var scenario = CheckInScenarioFactory.SeedCheckInScenario(seed, Guid.NewGuid(), withAssignment: false);
                stopCallId = scenario.BoardingCall.Id;
                var device = new ScannerDevice { DeviceCode = "DEV-AFTER-EMAILOUTBOX", IsActive = true };
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

            // DB nguồn đã có EmailOutbox từ trước (schema THẬT của main.latest) - nâng cấp bằng
            // assembly của nhánh này chỉ còn đúng 2 migration ScannerAssignment là pending.
            await using (var c = Context(connectionString))
            {
                var pendingBeforeUpgrade = (await c.Database.GetPendingMigrationsAsync()).ToList();
                // Migration phát sinh SAU 2 migration ScannerAssignment (vd AddAudioPlaybackRequests) cũng pending nhưng không thuộc tiền đề của test này.
                pendingBeforeUpgrade.Take(2).Should().Equal(
                    "20261106031430_AddScannerAssignmentOpenEndedPrimaryUniqueIndex",
                    "20261106090321_RestoreScannerAssignmentTripStopCallIdIndex");

                await c.Database.MigrateAsync();
                (await c.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
            }

            await using var check = Context(connectionString);
            (await check.ScannerAssignments.FindAsync(assignmentId)).Should().NotBeNull(
                "dữ liệu ScannerAssignment đã tồn tại TRƯỚC 2 migration mới (trên schema main.latest thật) phải còn nguyên sau khi nâng cấp");

            (await TableExistsAsync(check, "EmailOutbox")).Should().BeTrue(
                "bảng EmailOutbox của PR #59 (đã có từ trước) phải còn nguyên sau khi áp thêm 2 migration ScannerAssignment");

            var indexes = await IndexesOnScannerAssignmentsAsync(check);

            var generalIndex = indexes.SingleOrDefault(i => i.Name == "IX_ScannerAssignments_TripStopCallId");
            generalIndex.Should().NotBeNull("index chung (mọi bản ghi) phải tồn tại sau khi nâng cấp");
            generalIndex!.IsUnique.Should().BeFalse("index chung KHÔNG được là unique");
            generalIndex.FilterDefinition.Should().BeNull("index chung không được có filter");

            var filteredIndex = indexes.SingleOrDefault(i => i.Name == "IX_ScannerAssignments_TripStopCallId_OpenEndedPrimary");
            filteredIndex.Should().NotBeNull("index filtered unique (CHK-03) phải tồn tại sau khi nâng cấp");
            filteredIndex!.IsUnique.Should().BeTrue("index CHK-03 phải là UNIQUE");
            filteredIndex.FilterDefinition.Should().NotBeNullOrEmpty("index CHK-03 phải có filter");
            filteredIndex.FilterDefinition.Should().Contain("IsPrimaryOffline").And.Contain("ActiveUntil").And.Contain("IsDeleted");
        }
        finally
        {
            await CleanupBaselineDatabaseAsync(connectionString);
        }
    }

    [MainBaselineDuplicateFact]
    public async Task UpgradingWithPreExistingDuplicateOpenEndedPrimaries_ShouldFailWithUniqueKeyViolationNotSilentlyCorrupt()
    {
        // Review: "xác nhận dữ liệu cũ có vi phạm unique index mới không" - mô phỏng ĐÚNG tình
        // huống xấu nhất: DB cũ (schema thật của main tại SHA cố định bb1bf0c, trước PR #58, chưa
        // có index này) do CHÍNH race đang được sửa (CHK-03) đã lỡ để lại 2 Primary Offline Scanner
        // vô thời hạn cho CÙNG 1 điểm dừng. Áp migration tạo unique index PHẢI thất bại rõ ràng - KHÔNG được âm
        // thầm xoá/sửa 1 trong 2 bản ghi lịch sử để migration "qua được" (BR v2.0: không tự ý sửa
        // dữ liệu nghiệp vụ đã có mà không qua đối soát rõ ràng). Đây là hành vi ĐÚNG cần xác nhận.
        var connectionString = Environment.GetEnvironmentVariable("WATERBUS_TEST_MAIN_BASELINE_DUPLICATE")!;
        try
        {
            Guid stopCallId;
            await using (var seed = SeedContext(connectionString))
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

            await using var c2 = Context(connectionString);

            // SqlException cụ thể với mã lỗi 1505 ("CREATE UNIQUE INDEX terminated because a
            // duplicate key was found") - LƯU Ý: đây KHÁC với 2601/2627 (chỉ xảy ra khi INSERT/
            // UPDATE vi phạm 1 unique index ĐÃ TỒN TẠI từ trước, như trong EfUnitOfWork.cs cho race
            // CHK-02/CHK-06). Ở đây tình huống ngược lại: chính câu lệnh DDL "CREATE UNIQUE INDEX"
            // của migration phát hiện dữ liệu ĐÃ CÓ SẴN bị trùng trước khi index kịp tồn tại - SQL
            // Server báo lỗi 1505 cho đúng trường hợp này (đã xác nhận bằng CI thật, không phải
            // đoán). KHÔNG assert Exception chung để tránh che giấu 1 lỗi hoàn toàn khác (vd lỗi cú
            // pháp migration, mất kết nối) mà vẫn làm test "pass" sai ý nghĩa. Tự dò qua
            // InnerException (không giả định EF bọc hay không bọc SqlException gốc) để chắc chắn
            // bắt đúng exception thật.
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

            await using var check = Context(connectionString);

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
        finally
        {
            await CleanupBaselineDatabaseAsync(connectionString);
        }
    }
}
