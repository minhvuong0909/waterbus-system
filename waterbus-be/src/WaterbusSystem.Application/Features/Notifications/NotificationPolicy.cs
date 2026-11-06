using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Application.Features.Notifications;

/// <summary>
/// Chính sách theo loại thông báo. Tài chính (xác nhận thanh toán/vé, hoàn tiền) có số lần thử và thời hạn dài hơn.
/// Không loại nào bị tự động huỷ vì tuổi: quá hạn ⇒ DeadLetter (StaleNeedsReview) để vận hành quyết định gửi lại hay không.
/// </summary>
public static class NotificationPolicy
{
    public static bool IsFinancial(EmailNotificationType type) =>
        type is EmailNotificationType.BookingConfirmed or EmailNotificationType.RefundSucceeded;

    public static int MaxAttempts(EmailNotificationType type, EmailOptions o) =>
        Math.Max(1, IsFinancial(type) ? o.FinancialMaxAttempts : o.MaxAttempts);

    public static TimeSpan ReviewAfter(EmailNotificationType type, EmailOptions o) =>
        TimeSpan.FromHours(Math.Max(1, IsFinancial(type) ? o.FinancialMaxAgeHours : o.MaxMessageAgeHours));
}
