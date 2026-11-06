using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WaterbusSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUniquePrimaryScannerPerTripStopCall : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1
                    FROM [ScannerAssignments]
                    WHERE [IsPrimaryOffline] = 1
                    GROUP BY [TripStopCallId]
                    HAVING COUNT(*) > 1
                )
                    THROW 51000, 'Resolve duplicate primary scanner assignments before applying this migration.', 1;
                """);

            migrationBuilder.DropIndex(
                name: "IX_ScannerAssignments_TripStopCallId",
                table: "ScannerAssignments");

            migrationBuilder.CreateIndex(
                name: "IX_ScannerAssignments_TripStopCallId",
                table: "ScannerAssignments",
                column: "TripStopCallId",
                unique: true,
                filter: "[IsPrimaryOffline] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ScannerAssignments_TripStopCallId",
                table: "ScannerAssignments");

            migrationBuilder.CreateIndex(
                name: "IX_ScannerAssignments_TripStopCallId",
                table: "ScannerAssignments",
                column: "TripStopCallId");
        }
    }
}
