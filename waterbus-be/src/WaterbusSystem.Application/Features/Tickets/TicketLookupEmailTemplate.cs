using WaterbusSystem.Application.Features.Notifications.Templates;

namespace WaterbusSystem.Application.Features.Tickets;

/// <summary>Email OTP tra cứu vé. Chỉ chứa OTP và thời hạn; không có mã đặt chỗ, QR hay thông tin chuyến.</summary>
public static class TicketLookupEmailTemplate
{
    public static NotificationTemplates.Rendered Render(string purchaserName, string otp, TimeSpan validFor, EmailContext ctx)
    {
        var minutes = Math.Max(1, (int)Math.Ceiling(validFor.TotalMinutes));
        var b = new EmailBodyBuilder(ctx);
        b.Paragraph($"Xin chào {EmailFormat.SingleLine(purchaserName)},");
        b.Paragraph("Bạn (hoặc ai đó) vừa yêu cầu tra cứu vé bằng email này. Mã xác thực của bạn:");
        b.Fact("Mã xác thực", otp);
        b.Fact("Hiệu lực", $"{minutes} phút");
        b.Notice("Không chia sẻ mã này với bất kỳ ai. Nếu bạn không yêu cầu tra cứu, hãy bỏ qua email này, vé của bạn vẫn an toàn.");
        var (html, text) = b.Render("Mã xác thực tra cứu vé");
        return new NotificationTemplates.Rendered("Mã xác thực tra cứu vé Smart Waterbus", html, text);
    }
}
