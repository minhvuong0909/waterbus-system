using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.UnitTests.Domain;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;

namespace WaterbusSystem.UnitTests.CheckIns;

[Collection(SqlCollections.Serial)]
public sealed class StationScannerMigrationSqlTests
{
    [SqlServerDomainFact]
    public async Task UpgradePreservesScannerAssignmentAndEventAudit_WithoutGuessingAStation()
    {
        var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("WATERBUS_TEST_SQLSERVER"))
        {
            InitialCatalog = "WaterbusDomainTests_StationScannerMigration_" + Guid.NewGuid().ToString("N")
        }.ConnectionString;
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connection).Options;
        await using var db = new ApplicationDbContext(options);
        try
        {
            await db.GetService<IMigrator>().MigrateAsync("20261115113530_AddPurchaseOrderLinkingIndexes");
            var staffId = Guid.NewGuid();
            var scannerId = Guid.NewGuid();
            var legacyEvent = Guid.NewGuid();
            var legacyAssignment = Guid.NewGuid();
            var scenario = CheckInScenarioFactory.SeedCheckInScenario(db, staffId, withAssignment: false);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO ScannerDevices (Id, DeviceCode, Label, IsActive, CreatedAt, IsDeleted)
                    VALUES ({scannerId}, 'LEGACY-BD', 'Gate old', 1, {scenario.Now}, 0);
                INSERT INTO ScannerAssignments (Id, DeviceId, StaffAccountId, TripStopCallId, IsPrimaryOffline, ActiveFrom, CreatedAt, IsDeleted)
                    VALUES ({legacyAssignment}, {scannerId}, {staffId}, {scenario.BoardingCall.Id}, 1, {scenario.Now}, {scenario.Now}, 0);
                INSERT INTO CheckInEvents (Id, TicketId, StaffAccountId, ScannerDeviceId, TripStopCallId, ClientEventId, OccurredAtDevice,
                    Outcome, SyncStatus, CreatedAt, IsDeleted)
                    VALUES ({legacyEvent}, {scenario.Ticket.Id}, {staffId}, {scannerId}, {scenario.BoardingCall.Id},
                        'legacy-client-event', {scenario.Now}, 'Success', 'Synced', {scenario.Now}, 0);
                """);
            await db.Database.MigrateAsync();

            var scanner = await db.ScannerDevices.AsNoTracking().SingleAsync(s => s.Id == scannerId);
            var assignment = await db.ScannerAssignments.AsNoTracking().SingleAsync(a => a.Id == legacyAssignment);
            var evt = await db.CheckInEvents.AsNoTracking().SingleAsync(e => e.Id == legacyEvent);
            Assert.Equal("LEGACY-BD", scanner.DeviceCode);
            Assert.Null(scanner.StationId);
            Assert.Empty(await db.ScannerEnrollments.ToListAsync());
            Assert.Equal(staffId, assignment.StaffAccountId);
            Assert.Equal(scannerId, evt.ScannerDeviceId);
            Assert.Null(evt.ScannerEnrollmentId);
            Assert.Equal("Success", evt.Outcome);
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }
}
