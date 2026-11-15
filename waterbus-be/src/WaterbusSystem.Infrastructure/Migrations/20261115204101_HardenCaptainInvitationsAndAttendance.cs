using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WaterbusSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HardenCaptainInvitationsAndAttendance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "ActedByAdminId",
                table: "CaptainAuditEvent",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "ActedByCaptainId",
                table: "CaptainAuditEvent",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PersonnelInvitationPending",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Trips_Status_ArrivalTime",
                table: "Trips",
                columns: new[] { "Status", "ArrivalTime" },
                filter: "[IsDeleted] = 0")
                .Annotation("SqlServer:Include", new[] { "BoatId" });

            // Khong hop thuc hoa bang giay phep gia: ghi canh bao ben vung cho tung Trip bi anh huong.
            migrationBuilder.Sql("""
                INSERT INTO CaptainAlert
                  (Id,DeduplicationKey,TripId,BoatId,CaptainId,Type,Message,OccurredAt,CreatedAt,IsDeleted)
                SELECT NEWID(), CONCAT('profile:',LOWER(CONVERT(char(36),b.Id)),':',LOWER(CONVERT(char(36),t.Id))),
                  t.Id,b.Id,b.CaptainUserId,'CaptainProfileRequired',
                  N'Legacy Captain profile requires Admin review and real license data before sales cutover. Review pending payments.',
                  SYSUTCDATETIME(),SYSUTCDATETIME(),0
                FROM Trips t JOIN Boats b ON b.Id=t.BoatId AND b.IsDeleted=0
                LEFT JOIN AspNetUsers u ON u.Id=b.CaptainUserId
                WHERE t.IsDeleted=0 AND
                  ((t.Status IN (1,2) AND t.ArrivalTime>=SYSUTCDATETIME()) OR EXISTS
                    (SELECT 1 FROM Bookings bk JOIN PurchaseOrders o ON o.Id=bk.OrderId
                     WHERE bk.TripId=t.Id AND bk.IsDeleted=0 AND o.Status IN ('Draft','PendingPayment')))
                  AND (u.Id IS NULL OR u.IsActive=0 OR ISNULL(u.CaptainStatus,3)<>1 OR
                    NULLIF(LTRIM(RTRIM(u.CaptainLicenseNumber)),'') IS NULL OR
                    u.CaptainLicenseExpiresOn IS NULL OR u.CaptainLicenseExpiresOn<CONVERT(date,DATEADD(hour,7,SYSUTCDATETIME())) OR
                    NULLIF(LTRIM(RTRIM(u.FullName)),'') IS NULL OR NULLIF(LTRIM(RTRIM(u.Email)),'') IS NULL OR
                    NULLIF(LTRIM(RTRIM(u.PhoneNumber)),'') IS NULL OR NOT EXISTS
                      (SELECT 1 FROM AspNetUserRoles ur JOIN AspNetRoles r ON r.Id=ur.RoleId WHERE ur.UserId=u.Id AND r.Name='Captain'))
                  AND NOT EXISTS (SELECT 1 FROM CaptainAlert a WHERE a.DeduplicationKey=
                    CONCAT('profile:',LOWER(CONVERT(char(36),b.Id)),':',LOWER(CONVERT(char(36),t.Id))));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM CaptainAuditEvent WHERE ActedByAdminId IS NULL)
                   OR EXISTS (SELECT 1 FROM AspNetUsers WHERE PersonnelInvitationPending=1)
                   THROW 51012, 'Cannot downgrade: preserve personnel invitation and activation audit history.', 1;
                """);
            migrationBuilder.DropIndex(
                name: "IX_Trips_Status_ArrivalTime",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "ActedByCaptainId",
                table: "CaptainAuditEvent");

            migrationBuilder.DropColumn(
                name: "PersonnelInvitationPending",
                table: "AspNetUsers");

            migrationBuilder.AlterColumn<Guid>(
                name: "ActedByAdminId",
                table: "CaptainAuditEvent",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
