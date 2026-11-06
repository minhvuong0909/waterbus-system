using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Application.Features.Notifications.Templates;

/// <summary>Nội dung 3 loại email (tiếng Việt). Chỉ dùng dữ liệu đã commit; không khẳng định điều hệ thống chưa xác nhận.</summary>
public static class NotificationTemplates
{
    public sealed record Rendered(string Subject, string Html, string Text);

    /// <param name="qrContentIds">BookingId → Content-ID của ảnh QR đã ký cho Booking đó.</param>
    public static Rendered BookingConfirmed(BookingConfirmationData d, EmailContext ctx, IReadOnlyDictionary<Guid, string> qrContentIds)
    {
        var b = new EmailBodyBuilder(ctx);
        var codes = string.Join(", ", d.Legs.Select(l => l.BookingCode));
        b.Paragraph($"Xin chào {d.PurchaserName},");
        b.Paragraph("Thanh toán của bạn đã được hệ thống xác nhận và vé điện tử đã được phát hành. Cảm ơn bạn đã đặt vé.");
        b.Heading("Thông tin đơn hàng");
        b.Fact("Mã đơn hàng", d.OrderId.ToString());
        b.Fact("Mã đặt chỗ", codes);
        b.Fact("Hình thức", d.PurchaseMode == PurchaseMode.RoundTrip ? "Khứ hồi" : "Một chiều");
        b.Fact("Trạng thái thanh toán", "Đã thanh toán");
        b.Fact("Tổng số tiền đã thanh toán", EmailFormat.Money(d.Total, d.Currency));

        var index = 0;
        foreach (var leg in d.Legs)
        {
            index++;
            b.Divider();
            b.Heading(d.Legs.Count > 1 ? $"Chặng {index}: {leg.FromStation} → {leg.ToStation}" : $"Hành trình: {leg.FromStation} → {leg.ToStation}");
            LegFacts(b, leg);
            b.Fact("Số tiền của chặng này", EmailFormat.Money(leg.PaidAmount, d.Currency));
            Passengers(b, leg.Passengers, d.Currency, showAmount: false);
            if (qrContentIds.TryGetValue(leg.BookingId, out var cid))
            {
                b.Paragraph($"Mã QR lên tàu cho đặt chỗ {leg.BookingCode} (dùng chung cho tất cả hành khách của đặt chỗ này):");
                b.InlineImage(cid, $"Mã QR lên tàu {leg.BookingCode}", 220,
                    "[Mã QR được đính kèm trong email; hãy mở bằng ứng dụng email hỗ trợ hiển thị ảnh.]");
            }
        }

        b.Divider();
        b.Heading("Cách sử dụng vé");
        b.Paragraph("Đưa mã QR cho nhân viên soát vé khi lên tàu. Mã QR là vé của bạn — không chia sẻ cho người không đi cùng.");
        b.Notice("Hãy lưu lại mã đặt chỗ. Email này không chứa liên kết quản lý đơn; vé cũng có trong mục \"Vé của tôi\" trên thiết bị và trình duyệt bạn đã dùng để đặt vé (không tự mở được trên thiết bị khác).");
        if (ctx.PublicBaseUrl is not null) b.Link("Mở \"Vé của tôi\" (trên thiết bị đã đặt vé)", ctx.PublicBaseUrl + "/tickets");
        var (html, text) = b.Render("Xác nhận đặt vé thành công");
        return new($"Vé điện tử của bạn – {codes}", html, text);
    }

    public static Rendered TripDisruption(TripDisruptionData d, bool terminated, EmailContext ctx)
    {
        var leg = d.Leg;
        var b = new EmailBodyBuilder(ctx);
        b.Paragraph($"Xin chào {d.PurchaserName},");
        b.Paragraph(terminated
            ? "Chuyến tàu trong đặt chỗ dưới đây đã bị chấm dứt giữa hành trình và không thể tiếp tục phục vụ."
            : "Chuyến tàu trong đặt chỗ dưới đây đã bị hủy và sẽ không khởi hành.");
        b.Heading("Chuyến bị ảnh hưởng");
        b.Fact("Mã đặt chỗ", leg.BookingCode);
        b.Fact("Mã đơn hàng", d.OrderId.ToString());
        b.Fact("Tình trạng chuyến", terminated ? "Đã chấm dứt giữa hành trình" : "Đã hủy");
        LegFacts(b, leg);
        Passengers(b, leg.Passengers, d.Currency, showAmount: false);
        b.Divider();
        b.Heading("Bước tiếp theo");
        b.Paragraph("Vé của đặt chỗ này không còn dùng được để lên chuyến tàu trên.");
        b.Paragraph(terminated
            ? "Khả năng được hoàn tiền phụ thuộc vào việc đoạn đi đã được phục vụ đến bến xuống hay chưa; bộ phận hỗ trợ sẽ xác nhận khi bạn liên hệ."
            : "Bạn có thể yêu cầu hoàn tiền cho đặt chỗ này.");
        b.Notice($"Email này KHÔNG xác nhận rằng khoản hoàn tiền đã được thực hiện. Để yêu cầu hoàn tiền, {Lower(b.SupportSentence())} và cung cấp mã đặt chỗ {leg.BookingCode}. Bạn sẽ nhận email riêng khi khoản hoàn tiền được xác nhận thành công.");
        var (html, text) = b.Render(terminated ? "Chuyến tàu bị chấm dứt giữa hành trình" : "Chuyến tàu của bạn đã bị hủy");
        return new(terminated ? $"Chuyến tàu bị chấm dứt giữa hành trình – {leg.BookingCode}" : $"Chuyến tàu bị hủy – {leg.BookingCode}", html, text);
    }

    public static Rendered RefundSucceeded(RefundSuccessData d, EmailContext ctx)
    {
        var leg = d.Leg;
        var b = new EmailBodyBuilder(ctx);
        var partial = d.Amount < d.PaymentAmount;
        b.Paragraph($"Xin chào {d.PurchaserName},");
        b.Paragraph("Khoản hoàn tiền dưới đây đã được xác nhận hoàn tất thành công.");
        b.Heading("Chi tiết hoàn tiền");
        b.Fact("Mã đơn hàng", d.OrderId.ToString());
        b.Fact("Mã đặt chỗ được hoàn", leg.BookingCode);
        b.Fact("Trạng thái hoàn tiền", "Thành công");
        b.Fact("Số tiền đã hoàn", EmailFormat.Money(d.Amount, d.Currency));
        b.Fact("Loại hoàn tiền", partial ? $"Hoàn một phần (đơn hàng đã thanh toán {EmailFormat.Money(d.PaymentAmount, d.Currency)})" : "Hoàn toàn bộ số tiền đã thanh toán");
        if (!string.IsNullOrWhiteSpace(d.GatewayReference)) b.Fact("Mã giao dịch hoàn tiền", d.GatewayReference!);
        if (d.CompletedAt is { } done) b.Fact("Hoàn tất lúc", EmailFormat.Time(done));
        b.Divider();
        b.Heading("Đặt chỗ bị ảnh hưởng");
        LegFacts(b, leg);
        if (d.Allocations.Count > 0) Passengers(b, d.Allocations, d.Currency, showAmount: true);
        else b.Paragraph("Khoản hoàn này là hoàn toàn bộ khoản thanh toán nhận muộn của đặt chỗ; không có vé nào được phát hành.");
        b.Notice("Thời gian tiền hiển thị trong tài khoản/ thẻ của bạn phụ thuộc vào ngân hàng hoặc đơn vị phát hành thẻ.");
        var (html, text) = b.Render("Hoàn tiền thành công");
        return new($"Hoàn tiền thành công {EmailFormat.Money(d.Amount, d.Currency)} – {leg.BookingCode}", html, text);
    }

    private static void LegFacts(EmailBodyBuilder b, BookingLeg leg)
    {
        b.Fact("Tuyến", leg.RouteName);
        b.Fact("Bến lên tàu", leg.FromStation);
        b.Fact("Bến xuống tàu", leg.ToStation);
        b.Fact("Khởi hành dự kiến", EmailFormat.Time(leg.Departure));
        b.Fact("Đến nơi dự kiến", EmailFormat.Time(leg.Arrival));
    }

    private static void Passengers(EmailBodyBuilder b, IReadOnlyList<PassengerLine> passengers, string currency, bool showAmount)
    {
        if (passengers.Count == 0) return;
        b.Paragraph($"Hành khách ({passengers.Count}):");
        foreach (var p in passengers)
            b.Fact($"{p.PassengerName}", $"Ghế {p.SeatCode} ({p.SeatClass}) · Vé {p.TicketCode}" +
                (showAmount ? " · " + EmailFormat.Money(p.Amount, currency) : ""));
    }

    private static string Lower(string s) => s.Length == 0 ? s : char.ToLowerInvariant(s[0]) + s[1..];
}
