using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WaterbusSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase2_CommercialModelAndPricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BasePrice",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Seats");

            migrationBuilder.DropColumn(
                name: "PriceMultiplier",
                table: "Seats");

            migrationBuilder.AddColumn<Guid>(
                name: "SeatClassId",
                table: "Seats",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "ServiceType",
                table: "Routes",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompletedAt",
                table: "Bookings",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ConfirmedAt",
                table: "Bookings",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OrderId",
                table: "Bookings",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "PublicBookingId",
                table: "Bookings",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "QrCredentialVersion",
                table: "Bookings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "QrIssuedAt",
                table: "Bookings",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PurchaseOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PassengerAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PurchaserName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    PurchaserEmail = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    PurchaserPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PurchaseMode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    QuotedTotal = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ConfirmedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SeatClasses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeatClasses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FareRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TripType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SeatClassId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EffectiveTo = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByAdminId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FareRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FareRules_SeatClasses_SeatClassId",
                        column: x => x.SeatClassId,
                        principalTable: "SeatClasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Seats_SeatClassId",
                table: "Seats",
                column: "SeatClassId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_OrderId",
                table: "Bookings",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_PublicBookingId",
                table: "Bookings",
                column: "PublicBookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FareRules_SeatClassId",
                table: "FareRules",
                column: "SeatClassId");

            migrationBuilder.CreateIndex(
                name: "IX_FareRules_TripType_SeatClassId_EffectiveFrom",
                table: "FareRules",
                columns: new[] { "TripType", "SeatClassId", "EffectiveFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SeatClasses_Code",
                table: "SeatClasses",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_PurchaseOrders_OrderId",
                table: "Bookings",
                column: "OrderId",
                principalTable: "PurchaseOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Seats_SeatClasses_SeatClassId",
                table: "Seats",
                column: "SeatClassId",
                principalTable: "SeatClasses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_PurchaseOrders_OrderId",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_Seats_SeatClasses_SeatClassId",
                table: "Seats");

            migrationBuilder.DropTable(
                name: "FareRules");

            migrationBuilder.DropTable(
                name: "PurchaseOrders");

            migrationBuilder.DropTable(
                name: "SeatClasses");

            migrationBuilder.DropIndex(
                name: "IX_Seats_SeatClassId",
                table: "Seats");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_OrderId",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_PublicBookingId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "SeatClassId",
                table: "Seats");

            migrationBuilder.DropColumn(
                name: "ServiceType",
                table: "Routes");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "ConfirmedAt",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "OrderId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "PublicBookingId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "QrCredentialVersion",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "QrIssuedAt",
                table: "Bookings");

            migrationBuilder.AddColumn<decimal>(
                name: "BasePrice",
                table: "Trips",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "Category",
                table: "Seats",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "PriceMultiplier",
                table: "Seats",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }
    }
}
