using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WaterbusSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseOrderLinkingIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_PassengerAccountId_CreatedAt",
                table: "PurchaseOrders",
                columns: new[] { "PassengerAccountId", "CreatedAt" },
                descending: new[] { false, true },
                filter: "[PassengerAccountId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_PurchaserEmail_Unlinked",
                table: "PurchaseOrders",
                column: "PurchaserEmail",
                filter: "[PassengerAccountId] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_PassengerAccountId_CreatedAt",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_PurchaserEmail_Unlinked",
                table: "PurchaseOrders");
        }
    }
}
