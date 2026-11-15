using WaterbusSystem.Application.Features.Notifications.Templates;

namespace WaterbusSystem.Application.Features.Accounts;

/// <summary>Email của luồng tài khoản. Chỉ chứa liên kết xác minh và hạn dùng; không có thông tin đơn hàng.</summary>
public static class AccountEmailTemplates
{
    public const int VerificationValidHours = 24;

    public static NotificationTemplates.Rendered Verification(string fullName, string link, EmailContext ctx)
    {
        var b = new EmailBodyBuilder(ctx);
        b.Paragraph($"Xin chào {EmailFormat.SingleLine(fullName)},");
        b.Paragraph("Mở liên kết để xác minh email và đặt mật khẩu. Sau khi kích hoạt tài khoản, bạn có thể đăng nhập để xem các vé đã đặt bằng email này.");
        b.Link("Xác minh email và đặt mật khẩu", link);
        b.Notice($"Liên kết có hiệu lực {VerificationValidHours} giờ. Nếu bạn không tạo tài khoản, hãy bỏ qua email này.");
        var (html, text) = b.Render("Xác minh email tài khoản");
        return new NotificationTemplates.Rendered("Xác minh email tài khoản Smart Waterbus", html, text);
    }
}
