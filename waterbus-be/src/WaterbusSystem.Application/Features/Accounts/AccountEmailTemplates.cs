using WaterbusSystem.Application.Features.Notifications.Templates;

namespace WaterbusSystem.Application.Features.Accounts;

/// <summary>Email của luồng tài khoản. Chỉ chứa liên kết xác minh và hạn dùng; không có thông tin đơn hàng.</summary>
public static class AccountEmailTemplates
{
    public const int VerificationValidHours = 24;

    public static NotificationTemplates.Rendered PersonnelInvitation(string fullName, string link, double validHours, EmailContext ctx)
    {
        var b = new EmailBodyBuilder(ctx);
        b.Paragraph($"Xin chào {EmailFormat.SingleLine(fullName)},");
        b.Paragraph("Admin Smart Waterbus đã mời bạn kích hoạt tài khoản nhân sự. Hãy xác minh email và tự đặt mật khẩu để đăng nhập.");
        b.Link("Kích hoạt tài khoản và đặt mật khẩu", link);
        b.Notice($"Liên kết dùng một lần, có hiệu lực {validHours.ToString(System.Globalization.CultureInfo.InvariantCulture)} giờ. Nếu link hết hạn, hãy liên hệ Admin để gửi lại.");
        var (html, text) = b.Render("Kích hoạt tài khoản nhân sự");
        return new NotificationTemplates.Rendered("Lời mời kích hoạt tài khoản Smart Waterbus", html, text);
    }

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
