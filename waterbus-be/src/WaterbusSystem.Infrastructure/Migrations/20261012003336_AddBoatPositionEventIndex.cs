using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WaterbusSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBoatPositionEventIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BoatPositionEvents_BoatId",
                table: "BoatPositionEvents");

            migrationBuilder.CreateIndex(
                name: "IX_BoatPositionEvents_BoatId_ObservedAt",
                table: "BoatPositionEvents",
                columns: new[] { "BoatId", "ObservedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BoatPositionEvents_BoatId_ObservedAt",
                table: "BoatPositionEvents");

            migrationBuilder.CreateIndex(
                name: "IX_BoatPositionEvents_BoatId",
                table: "BoatPositionEvents",
                column: "BoatId");
        }
    }
}
