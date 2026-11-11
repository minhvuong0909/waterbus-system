using System.Globalization;
using System.Text;
using WaterbusSystem.Application.DTOs.Responses.Refunds;

namespace WaterbusSystem.Application.Features.Refunds;

/// <summary>
/// Dựng CSV cho nhân viên hoàn tiền: UTF-8 có BOM (Excel đọc đúng tiếng Việt), CRLF, mọi ô chuỗi được escape theo RFC 4180.
/// Chống CSV injection: ô chuỗi bắt đầu bằng <c>= + - @</c> hoặc tab/CR được thêm dấu <c>'</c> phía trước để Excel không coi là công thức
/// (số điện thoại dạng <c>+84…</c> vì vậy hiện kèm dấu <c>'</c>; đây là đánh đổi có chủ ý). Giờ hiển thị theo GMT+7.
/// </summary>
public static class RefundCsvWriter
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);
    private static readonly char[] NeedsQuoting = [',', '"', '\r', '\n'];
    private const string FormulaStarters = "=+-@\t\r";

    public static readonly IReadOnlyList<string> Headers =
    [
        "Mã hoàn tiền", "Mã đặt chỗ", "Mã đơn hàng", "Người mua", "Email", "Số điện thoại",
        "Số tiền", "Tiền tệ", "Mã vé", "Tuyến", "Giờ khởi hành (GMT+7)", "Lý do hoàn",
        "Mã tham chiếu VNPay (vnp_TxnRef)", "Mã giao dịch VNPay (vnp_TransactionNo)", "Thanh toán lúc (GMT+7)",
        "Trạng thái", "Tạo yêu cầu lúc (GMT+7)", "Xuất lần cuối (GMT+7)"
    ];

    public static byte[] Write(IEnumerable<RefundExportRow> rows)
    {
        var sb = new StringBuilder();
        AppendLine(sb, Headers);
        foreach (var r in rows)
        {
            AppendLine(sb,
            [
                r.RefundId.ToString(), r.BookingCode, r.OrderId.ToString(), r.PurchaserName, r.PurchaserEmail, r.PurchaserPhone ?? "",
                r.Amount.ToString("0.##", CultureInfo.InvariantCulture), r.Currency, string.Join("; ", r.TicketCodes),
                r.RouteName ?? "", Time(r.DepartureTime), Reason(r),
                r.MerchantReference ?? "", r.PaymentGatewayReference ?? "", Time(r.PaidAt),
                r.Status, Time(r.RequestedAt), Time(r.LastExportedAt)
            ]);
        }
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        return [.. encoding.GetPreamble(), .. encoding.GetBytes(sb.ToString())];
    }

    private static string Reason(RefundExportRow r) =>
        r.IsLatePayment ? "Tiền về sau khi đơn hết hạn (không phát vé)"
        : r.TripStatus switch
        {
            "Cancelled" => "Chuyến bị hủy",
            "Terminated" => "Chuyến bị chấm dứt giữa hành trình",
            _ => ""
        };

    private static string Time(DateTimeOffset? value) =>
        value?.ToOffset(VietnamOffset).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "";

    private static void AppendLine(StringBuilder sb, IReadOnlyList<string> cells)
    {
        for (var i = 0; i < cells.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(Cell(cells[i]));
        }
        sb.Append("\r\n");
    }

    /// <summary>Escape một ô chuỗi: chống công thức Excel rồi quote theo RFC 4180.</summary>
    public static string Cell(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        var text = FormulaStarters.Contains(value[0]) ? "'" + value : value;
        return text.IndexOfAny(NeedsQuoting) >= 0 ? "\"" + text.Replace("\"", "\"\"") + "\"" : text;
    }
}
