using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WaterbusSystem.Infrastructure.Migrations
{
    /// <summary>
    /// Review PR #58: migration AddScannerAssignmentOpenEndedPrimaryUniqueIndex (CHK-03) đã
    /// DropIndex IX_ScannerAssignments_TripStopCallId (index FK thường, bao toàn bộ bản ghi) khi
    /// thêm index filtered unique IX_..._OpenEndedPrimary - bỏ sót rằng index filtered chỉ phủ đúng
    /// tập con Primary mở, không dùng được cho truy vấn tổng quát theo TripStopCallId (vd liệt kê
    /// mọi scanner - Primary/secondary, hữu hạn/vô hạn - của 1 điểm dừng). Migration này CHỈ tạo lại
    /// index thường đã bị xoá nhầm - KHÔNG đụng đến index filtered unique (đã tồn tại đúng, generator
    /// `dotnet ef migrations add` tạo nhầm lệnh CreateIndex trùng lặp cho nó - xem PR #58 review).
    /// </summary>
    /// <inheritdoc />
    public partial class RestoreScannerAssignmentTripStopCallIdIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ScannerAssignments_TripStopCallId",
                table: "ScannerAssignments",
                column: "TripStopCallId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ScannerAssignments_TripStopCallId",
                table: "ScannerAssignments");
        }
    }
}
