using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WaterbusSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CompleteBookingJourneyDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Legacy bookings do not identify the actual boarding/disembarking calls.
            // Never guess journey identifiers or discard transaction history during upgrade.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [Bookings])
                    THROW 51000, 'Existing Bookings require an explicit journey backfill before CompleteBookingJourneyDomain.', 1;
                UPDATE [TripStopCalls] SET [Status] = 'Planned' WHERE [Status] = 'Pending';
                UPDATE [PurchaseOrders] SET [Status] = 'PendingPayment' WHERE [Status] = 'Pending';
                UPDATE [Tickets] SET [BoardingStatus] = 'NotCheckedIn' WHERE [BoardingStatus] IN ('NotBoarded', '');
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_FinancialTransactions_Bookings_RefundBookingId",
                table: "FinancialTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_FinancialTransactions_FinancialTransactions_OriginalPaymentId",
                table: "FinancialTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_SeatReservations_Bookings_BookingId",
                table: "SeatReservations");

            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_SeatReservations_SeatReservationId",
                table: "Tickets");

            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_Trips_TripId",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Trips_ScheduleId",
                table: "Trips");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_TripId",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_SeatReservations_BookingId",
                table: "SeatReservations");

            migrationBuilder.DropIndex(
                name: "IX_FinancialTransactions_OriginalPaymentId",
                table: "FinancialTransactions");

            migrationBuilder.DropIndex(
                name: "IX_FinancialTransactions_RefundBookingId",
                table: "FinancialTransactions");

            migrationBuilder.DropColumn(
                name: "TripId",
                table: "Tickets");

            migrationBuilder.RenameColumn(
                name: "TotalAmount",
                table: "Bookings",
                newName: "QuotedTotal");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CheckInCloseAt",
                table: "TripStopCalls",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CheckInOpenAt",
                table: "TripStopCalls",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ScheduleStopId",
                table: "TripStopCalls",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ActualArrivalTime",
                table: "Trips",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ActualDepartureTime",
                table: "Trips",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDelayed",
                table: "Trips",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SalesCloseAt",
                table: "Trips",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "GatewayRequestCreatedAt",
                table: "FinancialTransactions",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MerchantReference",
                table: "FinancialTransactions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BoardingCallId",
                table: "Bookings",
                type: "uniqueidentifier",
                nullable: false);

            migrationBuilder.AddColumn<Guid>(
                name: "DisembarkingCallId",
                table: "Bookings",
                type: "uniqueidentifier",
                nullable: false);

            migrationBuilder.AddColumn<decimal>(
                name: "PaidAllocation",
                table: "Bookings",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TripId",
                table: "Bookings",
                type: "uniqueidentifier",
                nullable: false);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_TripStopCalls_TripId_Id",
                table: "TripStopCalls",
                columns: new[] { "TripId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_SeatReservations_Id_BookingId",
                table: "SeatReservations",
                columns: new[] { "Id", "BookingId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_FinancialTransactions_Id_OrderId",
                table: "FinancialTransactions",
                columns: new[] { "Id", "OrderId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Bookings_Id_OrderId",
                table: "Bookings",
                columns: new[] { "Id", "OrderId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Bookings_Id_TripId",
                table: "Bookings",
                columns: new[] { "Id", "TripId" });

            migrationBuilder.CreateIndex(
                name: "IX_TripStopCalls_ScheduleStopId",
                table: "TripStopCalls",
                column: "ScheduleStopId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TripStopCalls_CheckInWindow",
                table: "TripStopCalls",
                sql: "[CheckInOpenAt] IS NULL OR [CheckInCloseAt] IS NULL OR [CheckInOpenAt] < [CheckInCloseAt]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TripStopCalls_Status",
                table: "TripStopCalls",
                sql: "[Status] IN ('Planned', 'Arrived', 'Departed', 'Skipped')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TripStopCalls_VisitOrder",
                table: "TripStopCalls",
                sql: "[VisitOrder] > 0");

            migrationBuilder.CreateIndex(
                name: "IX_Trips_ScheduleId_DepartureTime",
                table: "Trips",
                columns: new[] { "ScheduleId", "DepartureTime" },
                unique: true,
                filter: "[ScheduleId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Trips_Arrival",
                table: "Trips",
                sql: "[ArrivalTime] >= [DepartureTime] AND ([ActualArrivalTime] IS NULL OR [ActualDepartureTime] IS NULL OR [ActualArrivalTime] >= [ActualDepartureTime])");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Trips_SalesClose",
                table: "Trips",
                sql: "[SalesCloseAt] IS NULL OR ([SalesCloseAt] <= [DepartureTime] AND ([ActualDepartureTime] IS NULL OR [SalesCloseAt] <= [ActualDepartureTime]))");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_SeatReservationId_BookingId",
                table: "Tickets",
                columns: new[] { "SeatReservationId", "BookingId" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Tickets_BoardingStatus",
                table: "Tickets",
                sql: "[BoardingStatus] IN ('NotCheckedIn', 'CheckedIn', 'NoShow')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Tickets_Fares",
                table: "Tickets",
                sql: "[FaceFareSnapshot] >= 0 AND [PaidFareSnapshot] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_SeatReservations_BookingId_TripId",
                table: "SeatReservations",
                columns: new[] { "BookingId", "TripId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_SeatReservations_Amounts",
                table: "SeatReservations",
                sql: "[QuotedFare] >= 0 AND ([PaidAllocation] IS NULL OR [PaidAllocation] >= 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SeatReservations_Segment",
                table: "SeatReservations",
                sql: "[BoardingStopOrder] > 0 AND [DisembarkingStopOrder] > [BoardingStopOrder]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RefundTicketAllocations_Amount",
                table: "RefundTicketAllocations",
                sql: "[Amount] > 0");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialTransactions_OriginalPaymentId_OrderId",
                table: "FinancialTransactions",
                columns: new[] { "OriginalPaymentId", "OrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialTransactions_RefundBookingId_OrderId",
                table: "FinancialTransactions",
                columns: new[] { "RefundBookingId", "OrderId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_FinancialTransactions_Amount",
                table: "FinancialTransactions",
                sql: "[Amount] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FinancialTransactions_Kind",
                table: "FinancialTransactions",
                sql: "([TransactionType] = 'Payment' AND [RefundBookingId] IS NULL AND [OriginalPaymentId] IS NULL) OR ([TransactionType] = 'Refund' AND [RefundBookingId] IS NOT NULL AND [OriginalPaymentId] IS NOT NULL AND [OriginalPaymentId] <> [Id])");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FinancialTransactions_ManualEvidence",
                table: "FinancialTransactions",
                sql: "[TransactionType] <> 'Refund' OR [GatewayName] IS NULL OR [GatewayName] <> 'Manual' OR [Status] <> 'Succeeded' OR ([ManualProcessedByAdminId] IS NOT NULL AND LEN(LTRIM(RTRIM([ManualRefundEvidence]))) > 0 AND [ManualRefundEvidence] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FinancialTransactions_Status",
                table: "FinancialTransactions",
                sql: "[Status] IN ('Requested', 'Processing', 'Succeeded', 'Failed')");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_TripId_BoardingCallId",
                table: "Bookings",
                columns: new[] { "TripId", "BoardingCallId" });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_TripId_DisembarkingCallId",
                table: "Bookings",
                columns: new[] { "TripId", "DisembarkingCallId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Bookings_Amounts",
                table: "Bookings",
                sql: "[QuotedTotal] >= 0 AND ([PaidAllocation] IS NULL OR [PaidAllocation] >= 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Bookings_DistinctCalls",
                table: "Bookings",
                sql: "[BoardingCallId] <> [DisembarkingCallId]");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_TripStopCalls_TripId_BoardingCallId",
                table: "Bookings",
                columns: new[] { "TripId", "BoardingCallId" },
                principalTable: "TripStopCalls",
                principalColumns: new[] { "TripId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_TripStopCalls_TripId_DisembarkingCallId",
                table: "Bookings",
                columns: new[] { "TripId", "DisembarkingCallId" },
                principalTable: "TripStopCalls",
                principalColumns: new[] { "TripId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Trips_TripId",
                table: "Bookings",
                column: "TripId",
                principalTable: "Trips",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FinancialTransactions_Bookings_RefundBookingId_OrderId",
                table: "FinancialTransactions",
                columns: new[] { "RefundBookingId", "OrderId" },
                principalTable: "Bookings",
                principalColumns: new[] { "Id", "OrderId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FinancialTransactions_FinancialTransactions_OriginalPaymentId_OrderId",
                table: "FinancialTransactions",
                columns: new[] { "OriginalPaymentId", "OrderId" },
                principalTable: "FinancialTransactions",
                principalColumns: new[] { "Id", "OrderId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SeatReservations_Bookings_BookingId_TripId",
                table: "SeatReservations",
                columns: new[] { "BookingId", "TripId" },
                principalTable: "Bookings",
                principalColumns: new[] { "Id", "TripId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_SeatReservations_SeatReservationId_BookingId",
                table: "Tickets",
                columns: new[] { "SeatReservationId", "BookingId" },
                principalTable: "SeatReservations",
                principalColumns: new[] { "Id", "BookingId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TripStopCalls_ScheduleStops_ScheduleStopId",
                table: "TripStopCalls",
                column: "ScheduleStopId",
                principalTable: "ScheduleStops",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [Bookings])
                    THROW 51001, 'Downgrade with Bookings would discard journey and paid allocation data. Export/backfill before downgrade.', 1;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_TripStopCalls_TripId_BoardingCallId",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_TripStopCalls_TripId_DisembarkingCallId",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Trips_TripId",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_FinancialTransactions_Bookings_RefundBookingId_OrderId",
                table: "FinancialTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_FinancialTransactions_FinancialTransactions_OriginalPaymentId_OrderId",
                table: "FinancialTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_SeatReservations_Bookings_BookingId_TripId",
                table: "SeatReservations");

            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_SeatReservations_SeatReservationId_BookingId",
                table: "Tickets");

            migrationBuilder.DropForeignKey(
                name: "FK_TripStopCalls_ScheduleStops_ScheduleStopId",
                table: "TripStopCalls");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_TripStopCalls_TripId_Id",
                table: "TripStopCalls");

            migrationBuilder.DropIndex(
                name: "IX_TripStopCalls_ScheduleStopId",
                table: "TripStopCalls");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TripStopCalls_CheckInWindow",
                table: "TripStopCalls");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TripStopCalls_Status",
                table: "TripStopCalls");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TripStopCalls_VisitOrder",
                table: "TripStopCalls");

            migrationBuilder.DropIndex(
                name: "IX_Trips_ScheduleId_DepartureTime",
                table: "Trips");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Trips_Arrival",
                table: "Trips");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Trips_SalesClose",
                table: "Trips");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_SeatReservationId_BookingId",
                table: "Tickets");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Tickets_BoardingStatus",
                table: "Tickets");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Tickets_Fares",
                table: "Tickets");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_SeatReservations_Id_BookingId",
                table: "SeatReservations");

            migrationBuilder.DropIndex(
                name: "IX_SeatReservations_BookingId_TripId",
                table: "SeatReservations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SeatReservations_Amounts",
                table: "SeatReservations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SeatReservations_Segment",
                table: "SeatReservations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RefundTicketAllocations_Amount",
                table: "RefundTicketAllocations");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_FinancialTransactions_Id_OrderId",
                table: "FinancialTransactions");

            migrationBuilder.DropIndex(
                name: "IX_FinancialTransactions_OriginalPaymentId_OrderId",
                table: "FinancialTransactions");

            migrationBuilder.DropIndex(
                name: "IX_FinancialTransactions_RefundBookingId_OrderId",
                table: "FinancialTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FinancialTransactions_Amount",
                table: "FinancialTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FinancialTransactions_Kind",
                table: "FinancialTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FinancialTransactions_ManualEvidence",
                table: "FinancialTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FinancialTransactions_Status",
                table: "FinancialTransactions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Bookings_Id_OrderId",
                table: "Bookings");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Bookings_Id_TripId",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_TripId_BoardingCallId",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_TripId_DisembarkingCallId",
                table: "Bookings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Bookings_Amounts",
                table: "Bookings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Bookings_DistinctCalls",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "CheckInCloseAt",
                table: "TripStopCalls");

            migrationBuilder.DropColumn(
                name: "CheckInOpenAt",
                table: "TripStopCalls");

            migrationBuilder.DropColumn(
                name: "ScheduleStopId",
                table: "TripStopCalls");

            migrationBuilder.DropColumn(
                name: "ActualArrivalTime",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "ActualDepartureTime",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "IsDelayed",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "SalesCloseAt",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "GatewayRequestCreatedAt",
                table: "FinancialTransactions");

            migrationBuilder.DropColumn(
                name: "MerchantReference",
                table: "FinancialTransactions");

            migrationBuilder.DropColumn(
                name: "BoardingCallId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "DisembarkingCallId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "PaidAllocation",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "TripId",
                table: "Bookings");

            migrationBuilder.RenameColumn(
                name: "QuotedTotal",
                table: "Bookings",
                newName: "TotalAmount");

            migrationBuilder.AddColumn<Guid>(
                name: "TripId",
                table: "Tickets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Trips_ScheduleId",
                table: "Trips",
                column: "ScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_TripId",
                table: "Tickets",
                column: "TripId");

            migrationBuilder.CreateIndex(
                name: "IX_SeatReservations_BookingId",
                table: "SeatReservations",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialTransactions_OriginalPaymentId",
                table: "FinancialTransactions",
                column: "OriginalPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialTransactions_RefundBookingId",
                table: "FinancialTransactions",
                column: "RefundBookingId");

            migrationBuilder.AddForeignKey(
                name: "FK_FinancialTransactions_Bookings_RefundBookingId",
                table: "FinancialTransactions",
                column: "RefundBookingId",
                principalTable: "Bookings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FinancialTransactions_FinancialTransactions_OriginalPaymentId",
                table: "FinancialTransactions",
                column: "OriginalPaymentId",
                principalTable: "FinancialTransactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SeatReservations_Bookings_BookingId",
                table: "SeatReservations",
                column: "BookingId",
                principalTable: "Bookings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_SeatReservations_SeatReservationId",
                table: "Tickets",
                column: "SeatReservationId",
                principalTable: "SeatReservations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_Trips_TripId",
                table: "Tickets",
                column: "TripId",
                principalTable: "Trips",
                principalColumn: "Id");

            migrationBuilder.Sql("""
                UPDATE [TripStopCalls] SET [Status] = 'Pending' WHERE [Status] = 'Planned';
                UPDATE [PurchaseOrders] SET [Status] = 'Pending' WHERE [Status] = 'PendingPayment';
                UPDATE [Tickets] SET [BoardingStatus] = 'NotBoarded' WHERE [BoardingStatus] = 'NotCheckedIn';
                """);
        }
    }
}
