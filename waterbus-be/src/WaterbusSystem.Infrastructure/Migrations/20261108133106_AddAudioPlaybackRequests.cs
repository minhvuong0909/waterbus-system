using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WaterbusSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAudioPlaybackRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AudioPlaybackRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TripId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoutePoiKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VisitKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BoatId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PoiId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AudioGuideId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AudioAssetUri = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DurationSeconds = table.Column<int>(type: "int", nullable: true),
                    DistanceMeters = table.Column<double>(type: "float", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    ReservedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AcceptedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudioPlaybackRequests", x => x.Id);
                    table.CheckConstraint("CK_AudioPlaybackRequests_Attempts", "[Attempts] >= 0");
                    table.CheckConstraint("CK_AudioPlaybackRequests_Status", "[Status] BETWEEN 1 AND 6");
                    table.ForeignKey(
                        name: "FK_AudioPlaybackRequests_Trips_TripId",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AudioPlaybackRequests_Boat",
                table: "AudioPlaybackRequests",
                columns: new[] { "BoatId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UX_AudioPlaybackRequests_Visit",
                table: "AudioPlaybackRequests",
                columns: new[] { "TripId", "RoutePoiKey", "VisitKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AudioPlaybackRequests");
        }
    }
}
