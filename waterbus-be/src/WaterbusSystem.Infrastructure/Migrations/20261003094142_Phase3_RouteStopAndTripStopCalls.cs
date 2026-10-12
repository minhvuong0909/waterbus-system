using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WaterbusSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase3_RouteStopAndTripStopCalls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RouteStops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RouteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SequenceNo = table.Column<int>(type: "int", nullable: false),
                    CanBoard = table.Column<bool>(type: "bit", nullable: false),
                    CanAlight = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RouteStops", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RouteStops_Routes_RouteId",
                        column: x => x.RouteId,
                        principalTable: "Routes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RouteStops_Stations_StationId",
                        column: x => x.StationId,
                        principalTable: "Stations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleStops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScheduleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RouteStopId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VisitOrder = table.Column<int>(type: "int", nullable: false),
                    ArrivalOffsetMin = table.Column<int>(type: "int", nullable: true),
                    DepartureOffsetMin = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleStops", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduleStops_RouteStops_RouteStopId",
                        column: x => x.RouteStopId,
                        principalTable: "RouteStops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduleStops_Schedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "Schedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TripStopCalls",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TripId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RouteStopId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VisitOrder = table.Column<int>(type: "int", nullable: false),
                    EstimatedArrivalTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EstimatedDepartureTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ActualArrivalTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ActualDepartureTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripStopCalls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TripStopCalls_RouteStops_RouteStopId",
                        column: x => x.RouteStopId,
                        principalTable: "RouteStops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TripStopCalls_Trips_TripId",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RouteStops_RouteId_SequenceNo",
                table: "RouteStops",
                columns: new[] { "RouteId", "SequenceNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RouteStops_StationId",
                table: "RouteStops",
                column: "StationId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleStops_RouteStopId",
                table: "ScheduleStops",
                column: "RouteStopId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleStops_ScheduleId_VisitOrder",
                table: "ScheduleStops",
                columns: new[] { "ScheduleId", "VisitOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TripStopCalls_RouteStopId",
                table: "TripStopCalls",
                column: "RouteStopId");

            migrationBuilder.CreateIndex(
                name: "IX_TripStopCalls_TripId_VisitOrder",
                table: "TripStopCalls",
                columns: new[] { "TripId", "VisitOrder" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScheduleStops");

            migrationBuilder.DropTable(
                name: "TripStopCalls");

            migrationBuilder.DropTable(
                name: "RouteStops");
        }
    }
}
