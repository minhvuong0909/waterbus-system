using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WaterbusSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddScannerAssignmentOpenEndedPrimaryUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ScannerAssignments_TripStopCallId",
                table: "ScannerAssignments");

            migrationBuilder.CreateIndex(
                name: "IX_ScannerAssignments_TripStopCallId_OpenEndedPrimary",
                table: "ScannerAssignments",
                column: "TripStopCallId",
                unique: true,
                filter: "[IsPrimaryOffline] = 1 AND [ActiveUntil] IS NULL AND [IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ScannerAssignments_TripStopCallId_OpenEndedPrimary",
                table: "ScannerAssignments");

            migrationBuilder.CreateIndex(
                name: "IX_ScannerAssignments_TripStopCallId",
                table: "ScannerAssignments",
                column: "TripStopCallId");
        }
    }
}
