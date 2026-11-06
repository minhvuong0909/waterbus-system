using Microsoft.Extensions.Options;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Notifications.Templates;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Application.Features.Notifications;

/// <summary>
/// Dựng email từ dữ liệu ĐÃ COMMIT tại thời điểm gửi và kiểm tra điều kiện còn đúng
/// (Order còn Confirmed, refund còn Succeeded, Trip đúng trạng thái…). Không đúng ⇒ Skip, không bao giờ gửi
/// thông báo sai sự thật. QR được ký tại chỗ từ credential hiện hành, KHÔNG lưu trong outbox.
/// </summary>
public sealed class NotificationEmailComposer(INotificationDataRepository data, IBookingQrSigner signer,
    IQrImageRenderer qr, IOptions<EmailOptions> options)
{
    public async Task<ComposeResult> ComposeAsync(EmailOutboxMessage m, CancellationToken ct)
    {
        var ctx = EmailContext.From(options.Value);
        switch (m.NotificationType)
        {
            case EmailNotificationType.BookingConfirmed:
            {
                var d = await data.GetBookingConfirmationAsync(m.OrderId, ct);
                if (d is null) return ComposeResult.Skip("Order không còn tồn tại.");
                if (d.OrderStatus != OrderStatus.Confirmed || d.Legs.Count == 0 ||
                    d.Legs.Any(l => l.Passengers.Count == 0 || l.BookingStatus is not (BookingStatus.Confirmed or BookingStatus.Completed)))
                    return ComposeResult.Skip("Order không ở trạng thái Confirmed có vé hợp lệ.");
                var images = new List<InlineImage>();
                var cids = new Dictionary<Guid, string>();
                foreach (var leg in d.Legs)
                {
                    var credential = signer.Sign(leg.PublicBookingId, leg.QrCredentialVersion, leg.TripId, leg.QrIssuedAt);
                    var cid = $"qr-{leg.BookingId:N}@waterbus";
                    cids[leg.BookingId] = cid;
                    images.Add(new InlineImage(cid, qr.RenderPng(credential.Payload), "image/png", $"qr-{leg.BookingCode}.png"));
                }
                var r = NotificationTemplates.BookingConfirmed(d, ctx, cids);
                return Ready(m, r, images);
            }
            case EmailNotificationType.TripCancelled or EmailNotificationType.TripTerminated:
            {
                var terminated = m.NotificationType == EmailNotificationType.TripTerminated;
                if (m.BookingId is not { } bookingId) return ComposeResult.Skip("Thiếu BookingId.");
                var d = await data.GetTripDisruptionAsync(bookingId, ct);
                if (d is null) return ComposeResult.Skip("Booking không còn tồn tại.");
                if (d.Leg.TripStatus != (terminated ? TripStatus.Terminated : TripStatus.Cancelled))
                    return ComposeResult.Skip($"Trip không còn ở trạng thái {(terminated ? "Terminated" : "Cancelled")}.");
                return Ready(m, NotificationTemplates.TripDisruption(d, terminated, ctx), []);
            }
            case EmailNotificationType.RefundSucceeded:
            {
                if (m.FinancialTransactionId is not { } refundId) return ComposeResult.Skip("Thiếu mã giao dịch hoàn tiền.");
                var d = await data.GetRefundSuccessAsync(refundId, ct);
                if (d is null || d.Status != FinancialTransactionStatus.Succeeded)
                    return ComposeResult.Skip("Khoản hoàn tiền chưa ở trạng thái Succeeded đã xác minh.");
                return Ready(m, NotificationTemplates.RefundSucceeded(d, ctx), []);
            }
            default:
                return ComposeResult.Skip("Loại thông báo không được hỗ trợ.");
        }
    }

    private static ComposeResult Ready(EmailOutboxMessage m, NotificationTemplates.Rendered r, IReadOnlyList<InlineImage> images) =>
        ComposeResult.Ready(new OutboundEmail(m.Id, m.RecipientEmail, m.RecipientName,
            EmailFormat.SingleLine(r.Subject), r.Html, r.Text, images));
}
