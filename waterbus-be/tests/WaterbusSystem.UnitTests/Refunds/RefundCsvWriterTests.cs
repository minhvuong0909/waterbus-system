using System.Text;
using FluentAssertions;
using WaterbusSystem.Application.DTOs.Responses.Refunds;
using WaterbusSystem.Application.Features.Refunds;
using Xunit;

namespace WaterbusSystem.UnitTests.Refunds;

public sealed class RefundCsvWriterTests
{
    private static RefundExportRow Row(
        string name = "Nguyễn An", string email = "an@example.com", string? phone = "0901234567",
        decimal amount = 80000m, string[]? tickets = null, bool late = false, string tripStatus = "Cancelled",
        DateTimeOffset? departure = null, DateTimeOffset? lastExported = null) =>
        new(Guid.Parse("11111111-1111-4111-8111-111111111111"), "BK001", Guid.Parse("22222222-2222-4222-8222-222222222222"),
            name, email, phone, amount, "VND", tickets ?? ["T1", "T2"], "Bạch Đằng – Linh Đông",
            departure ?? new DateTimeOffset(2026, 11, 12, 8, 0, 0, TimeSpan.Zero), late, tripStatus,
            "TXNREF123", "14012345", new DateTimeOffset(2026, 11, 10, 3, 30, 0, TimeSpan.Zero),
            "Processing", new DateTimeOffset(2026, 11, 12, 1, 0, 0, TimeSpan.Zero), lastExported);

    private static string[] Lines(byte[] csv) =>
        Encoding.UTF8.GetString(csv).TrimStart('﻿').Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

    [Fact]
    public void Output_StartsWithUtf8Bom_AndUsesCrlfWithHeaderFirst()
    {
        var csv = RefundCsvWriter.Write([Row()]);

        csv.Take(3).Should().Equal(0xEF, 0xBB, 0xBF);
        var lines = Lines(csv);
        lines.Should().HaveCount(2);
        lines[0].Should().Be(string.Join(',', RefundCsvWriter.Headers));
    }

    [Fact]
    public void Row_HasAmountTicketsRouteReasonAndVietnamTimes()
    {
        var line = Lines(RefundCsvWriter.Write([Row()]))[1];

        line.Should().Contain("80000").And.Contain("T1; T2").And.Contain("TXNREF123").And.Contain("14012345");
        line.Should().Contain("Chuyến bị hủy");
        line.Should().Contain("2026-11-12 15:00");          // 08:00 UTC → 15:00 GMT+7
        line.Should().Contain("2026-11-10 10:30");          // giờ thanh toán
    }

    [Theory]
    [InlineData("Terminated", "Chuyến bị chấm dứt giữa hành trình")]
    [InlineData("Cancelled", "Chuyến bị hủy")]
    public void Reason_FollowsTripStatus(string status, string expected) =>
        Lines(RefundCsvWriter.Write([Row(tripStatus: status)]))[1].Should().Contain(expected);

    [Fact]
    public void LatePayment_IsLabelledAsSuch_NotAsATripReason()
    {
        var line = Lines(RefundCsvWriter.Write([Row(late: true, tickets: [])]))[1];
        line.Should().Contain("Tiền về sau khi đơn hết hạn");
        line.Should().NotContain("Chuyến bị hủy");
    }

    [Theory]
    [InlineData("Nguyễn, Văn \"An\"", "\"Nguyễn, Văn \"\"An\"\"\"")]       // dấu phẩy và nháy kép
    [InlineData("Dòng 1\nDòng 2", "\"Dòng 1\nDòng 2\"")]                      // xuống dòng
    [InlineData("Bình thường", "Bình thường")]
    public void Cells_AreEscapedPerRfc4180(string input, string expected) =>
        RefundCsvWriter.Cell(input).Should().Be(expected);

    [Theory]
    [InlineData("=HYPERLINK(\"http://evil\")", "'=HYPERLINK")]
    [InlineData("+84901234567", "'+84901234567")]
    [InlineData("-2+3", "'-2+3")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("\tcmd", "'\tcmd")]
    public void FormulaStarters_AreNeutralised(string input, string expectedStart) =>
        RefundCsvWriter.Cell(input).TrimStart('"').Should().StartWith(expectedStart);

    [Fact]
    public void PurchaserNameThatLooksLikeAFormula_NeverStartsACell()
    {
        var line = Lines(RefundCsvWriter.Write([Row(name: "=cmd|' /C calc'!A0")]))[1];
        line.Should().NotContain(",=cmd").And.Contain(",'=cmd");
    }

    [Fact]
    public void Amount_UsesInvariantFormat_WithoutThousandsSeparator_AndKeepsCents()
    {
        Lines(RefundCsvWriter.Write([Row(amount: 1234567m)]))[1].Should().Contain(",1234567,");
        Lines(RefundCsvWriter.Write([Row(amount: 15000.5m)]))[1].Should().Contain(",15000.5,");
    }

    [Fact]
    public void MissingOptionalValues_BecomeEmptyCells()
    {
        var csv = RefundCsvWriter.Write([Row(phone: null, lastExported: null)]);
        var cells = Lines(csv)[1].Split(',');
        cells.Should().HaveCount(RefundCsvWriter.Headers.Count);
        cells[^1].Should().BeEmpty();                       // chưa xuất lần nào
    }

    [Fact]
    public void NoRows_YieldsHeaderOnly() => Lines(RefundCsvWriter.Write([])).Should().ContainSingle();
}
