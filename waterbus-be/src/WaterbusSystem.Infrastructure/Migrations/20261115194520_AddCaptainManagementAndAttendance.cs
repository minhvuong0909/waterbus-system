using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WaterbusSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCaptainManagementAndAttendance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CaptainAssignmentVersion",
                table: "Boats",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateOnly>(
                name: "CaptainLicenseExpiresOn",
                table: "AspNetUsers",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CaptainLicenseNumber",
                table: "AspNetUsers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CaptainSessionVersion",
                table: "AspNetUsers",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "CaptainStatus",
                table: "AspNetUsers",
                type: "int",
                nullable: true);

            migrationBuilder.Sql("UPDATE [Boats] SET [CaptainAssignmentVersion] = NEWID(); UPDATE [AspNetUsers] SET [CaptainSessionVersion] = NEWID();");

            migrationBuilder.CreateTable(
                name: "CaptainAlert",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeduplicationKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TripId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BoatId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaptainId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ResolvedByAdminId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaptainAlert", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaptainAlert_Boats_BoatId",
                        column: x => x.BoatId,
                        principalTable: "Boats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaptainAlert_Trips_TripId",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CaptainAttendance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TripId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BoatId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaptainId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignmentVersion = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordedByAdminId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RequiredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsLate = table.Column<bool>(type: "bit", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaptainAttendance", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaptainAttendance_Boats_BoatId",
                        column: x => x.BoatId,
                        principalTable: "Boats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaptainAttendance_Trips_TripId",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CaptainAuditEvent",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaptainId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BoatId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TripId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActedByAdminId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OldValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValues = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaptainAuditEvent", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaptainAuditEvent_Boats_BoatId",
                        column: x => x.BoatId,
                        principalTable: "Boats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaptainAuditEvent_Trips_TripId",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_CaptainLicenseNumber",
                table: "AspNetUsers",
                column: "CaptainLicenseNumber",
                unique: true,
                filter: "[CaptainLicenseNumber] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_CaptainStatus",
                table: "AspNetUsers",
                sql: "[CaptainStatus] IS NULL OR [CaptainStatus] IN (1,2,3)");

            migrationBuilder.CreateIndex(
                name: "IX_CaptainAlert_BoatId",
                table: "CaptainAlert",
                column: "BoatId");

            migrationBuilder.CreateIndex(
                name: "IX_CaptainAlert_DeduplicationKey",
                table: "CaptainAlert",
                column: "DeduplicationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CaptainAlert_TripId",
                table: "CaptainAlert",
                column: "TripId");

            migrationBuilder.CreateIndex(
                name: "IX_CaptainAttendance_BoatId",
                table: "CaptainAttendance",
                column: "BoatId");

            migrationBuilder.CreateIndex(
                name: "IX_CaptainAttendance_TripId_BoatId_AssignmentVersion",
                table: "CaptainAttendance",
                columns: new[] { "TripId", "BoatId", "AssignmentVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CaptainAuditEvent_BoatId",
                table: "CaptainAuditEvent",
                column: "BoatId");

            migrationBuilder.CreateIndex(
                name: "IX_CaptainAuditEvent_CaptainId_OccurredAt",
                table: "CaptainAuditEvent",
                columns: new[] { "CaptainId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CaptainAuditEvent_TripId",
                table: "CaptainAuditEvent",
                column: "TripId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CaptainAlert");

            migrationBuilder.DropTable(
                name: "CaptainAttendance");

            migrationBuilder.DropTable(
                name: "CaptainAuditEvent");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_CaptainLicenseNumber",
                table: "AspNetUsers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_CaptainStatus",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "CaptainAssignmentVersion",
                table: "Boats");

            migrationBuilder.DropColumn(
                name: "CaptainLicenseExpiresOn",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "CaptainLicenseNumber",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "CaptainSessionVersion",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "CaptainStatus",
                table: "AspNetUsers");
        }
    }
}
