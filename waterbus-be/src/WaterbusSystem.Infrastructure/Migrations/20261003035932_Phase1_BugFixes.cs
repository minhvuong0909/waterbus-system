using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WaterbusSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase1_BugFixes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Trips_Schedules_ScheduleId",
                table: "Trips");

            migrationBuilder.AlterColumn<string>(
                name: "CustomerPhone",
                table: "Bookings",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AddForeignKey(
                name: "FK_Trips_Schedules_ScheduleId",
                table: "Trips",
                column: "ScheduleId",
                principalTable: "Schedules",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Trips_Schedules_ScheduleId",
                table: "Trips");

            migrationBuilder.AlterColumn<string>(
                name: "CustomerPhone",
                table: "Bookings",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Trips_Schedules_ScheduleId",
                table: "Trips",
                column: "ScheduleId",
                principalTable: "Schedules",
                principalColumn: "Id");
        }
    }
}
