using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WaterbusSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStationBoundScanners : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "InvitationExpiresAt",
                table: "ScannerDevices",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvitationHash",
                table: "ScannerDevices",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RevokedAt",
                table: "ScannerDevices",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StationId",
                table: "ScannerDevices",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SuspendedAt",
                table: "ScannerDevices",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "StaffAccountId",
                table: "ScannerAssignments",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RevokedAt",
                table: "ScannerAssignments",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ScannerEnrollmentId",
                table: "CheckInEvents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ScannerEnrollments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScannerDeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CredentialHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PairedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScannerEnrollments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScannerEnrollments_ScannerDevices_ScannerDeviceId",
                        column: x => x.ScannerDeviceId,
                        principalTable: "ScannerDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScannerEnrollments_Stations_StationId",
                        column: x => x.StationId,
                        principalTable: "Stations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StaffStationShifts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StaffAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActiveFrom = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ActiveUntil = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffStationShifts", x => x.Id);
                    table.CheckConstraint("CK_StaffStationShifts_Window", "[ActiveUntil] > [ActiveFrom]");
                    table.ForeignKey(
                        name: "FK_StaffStationShifts_AspNetUsers_StaffAccountId",
                        column: x => x.StaffAccountId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffStationShifts_Stations_StationId",
                        column: x => x.StationId,
                        principalTable: "Stations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScannerOfflineGrants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EnrollmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StaffStationShiftId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScannerAssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActiveFrom = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ActiveUntil = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScannerOfflineGrants", x => x.Id);
                    table.CheckConstraint("CK_ScannerOfflineGrants_Window", "[ActiveUntil] > [ActiveFrom]");
                    table.ForeignKey(
                        name: "FK_ScannerOfflineGrants_ScannerAssignments_ScannerAssignmentId",
                        column: x => x.ScannerAssignmentId,
                        principalTable: "ScannerAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScannerOfflineGrants_ScannerEnrollments_EnrollmentId",
                        column: x => x.EnrollmentId,
                        principalTable: "ScannerEnrollments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScannerOfflineGrants_StaffStationShifts_StaffStationShiftId",
                        column: x => x.StaffStationShiftId,
                        principalTable: "StaffStationShifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScannerDevices_StationId",
                table: "ScannerDevices",
                column: "StationId");

            migrationBuilder.CreateIndex(
                name: "IX_CheckInEvents_ScannerEnrollmentId",
                table: "CheckInEvents",
                column: "ScannerEnrollmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ScannerEnrollments_Active",
                table: "ScannerEnrollments",
                column: "ScannerDeviceId",
                unique: true,
                filter: "[EndedAt] IS NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ScannerEnrollments_StationId",
                table: "ScannerEnrollments",
                column: "StationId");

            migrationBuilder.CreateIndex(
                name: "IX_ScannerOfflineGrants_EnrollmentId",
                table: "ScannerOfflineGrants",
                column: "EnrollmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ScannerOfflineGrants_ScannerAssignmentId",
                table: "ScannerOfflineGrants",
                column: "ScannerAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ScannerOfflineGrants_StaffStationShiftId",
                table: "ScannerOfflineGrants",
                column: "StaffStationShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffStationShifts_StaffAccountId_ActiveFrom",
                table: "StaffStationShifts",
                columns: new[] { "StaffAccountId", "ActiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_StaffStationShifts_StationId",
                table: "StaffStationShifts",
                column: "StationId");

            migrationBuilder.AddForeignKey(
                name: "FK_CheckInEvents_ScannerEnrollments_ScannerEnrollmentId",
                table: "CheckInEvents",
                column: "ScannerEnrollmentId",
                principalTable: "ScannerEnrollments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ScannerDevices_Stations_StationId",
                table: "ScannerDevices",
                column: "StationId",
                principalTable: "Stations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddColumn<string>(
                name: "ReviewNote",
                table: "CheckInEvents",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReviewedAt",
                table: "CheckInEvents",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedByAccountId",
                table: "CheckInEvents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ScannerOfflineGrantId",
                table: "CheckInEvents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CheckInEvents_ScannerOfflineGrantId",
                table: "CheckInEvents",
                column: "ScannerOfflineGrantId");

            migrationBuilder.AddForeignKey(
                name: "FK_CheckInEvents_ScannerOfflineGrants_ScannerOfflineGrantId",
                table: "CheckInEvents",
                column: "ScannerOfflineGrantId",
                principalTable: "ScannerOfflineGrants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM ScannerEnrollments)
                   OR EXISTS (SELECT 1 FROM StaffStationShifts)
                   OR EXISTS (SELECT 1 FROM ScannerAssignments WHERE StaffAccountId IS NULL)
                   OR EXISTS (SELECT 1 FROM ScannerDevices WHERE StationId IS NOT NULL)
                    THROW 51000, 'Restore a pre-cutover backup to roll back station-bound scanners with operational data.', 1;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_CheckInEvents_ScannerOfflineGrants_ScannerOfflineGrantId",
                table: "CheckInEvents");

            migrationBuilder.DropIndex(
                name: "IX_CheckInEvents_ScannerOfflineGrantId",
                table: "CheckInEvents");

            migrationBuilder.DropColumn(
                name: "ReviewNote",
                table: "CheckInEvents");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "CheckInEvents");

            migrationBuilder.DropColumn(
                name: "ReviewedByAccountId",
                table: "CheckInEvents");

            migrationBuilder.DropColumn(
                name: "ScannerOfflineGrantId",
                table: "CheckInEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_CheckInEvents_ScannerEnrollments_ScannerEnrollmentId",
                table: "CheckInEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_ScannerDevices_Stations_StationId",
                table: "ScannerDevices");

            migrationBuilder.DropTable(
                name: "ScannerOfflineGrants");

            migrationBuilder.DropTable(
                name: "ScannerEnrollments");

            migrationBuilder.DropTable(
                name: "StaffStationShifts");

            migrationBuilder.DropIndex(
                name: "IX_ScannerDevices_StationId",
                table: "ScannerDevices");

            migrationBuilder.DropIndex(
                name: "IX_CheckInEvents_ScannerEnrollmentId",
                table: "CheckInEvents");

            migrationBuilder.DropColumn(
                name: "InvitationExpiresAt",
                table: "ScannerDevices");

            migrationBuilder.DropColumn(
                name: "InvitationHash",
                table: "ScannerDevices");

            migrationBuilder.DropColumn(
                name: "RevokedAt",
                table: "ScannerDevices");

            migrationBuilder.DropColumn(
                name: "StationId",
                table: "ScannerDevices");

            migrationBuilder.DropColumn(
                name: "SuspendedAt",
                table: "ScannerDevices");

            migrationBuilder.DropColumn(
                name: "RevokedAt",
                table: "ScannerAssignments");

            migrationBuilder.DropColumn(
                name: "ScannerEnrollmentId",
                table: "CheckInEvents");

            migrationBuilder.AlterColumn<Guid>(
                name: "StaffAccountId",
                table: "ScannerAssignments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
