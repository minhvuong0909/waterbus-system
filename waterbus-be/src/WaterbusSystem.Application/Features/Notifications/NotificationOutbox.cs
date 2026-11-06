using System.Net.Mail;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Application.Features.Notifications;

public sealed class NotificationOutbox(IEmailOutboxRepository outbox, INotificationDataRepository data, IDateTimeService clock)
    : INotificationOutbox
{
    public async Task EnqueueBookingConfirmedAsync(Guid orderId, CancellationToken ct)
    {
        var recipient = await data.GetOrderRecipientAsync(orderId, ct);
        await AddOnceAsync($"BOOKING_CONFIRMED:{orderId:N}", EmailNotificationType.BookingConfirmed,
            recipient ?? new NotificationRecipient(orderId, null, "", ""), null, ct);
    }

    public async Task EnqueueTripDisruptionAsync(Guid tripId, EmailNotificationType type, CancellationToken ct)
    {
        var prefix = type switch
        {
            EmailNotificationType.TripCancelled => "TRIP_CANCELLED",
            EmailNotificationType.TripTerminated => "TRIP_TERMINATED",
            _ => throw new ArgumentOutOfRangeException(nameof(type), "Chỉ TripCancelled/TripTerminated.")
        };
        foreach (var recipient in await data.GetConfirmedBookingRecipientsForTripAsync(tripId, ct))
            await AddOnceAsync($"{prefix}:{recipient.BookingId:N}", type, recipient, null, ct);
    }

    public async Task EnqueueRefundSucceededAsync(Guid refundId, CancellationToken ct)
    {
        var recipient = await data.GetRefundRecipientAsync(refundId, ct);
        if (recipient is null) return; // refund không tồn tại: không có gì để thông báo
        await AddOnceAsync($"REFUND_SUCCEEDED:{refundId:N}", EmailNotificationType.RefundSucceeded, recipient, refundId, ct);
    }

    private async Task AddOnceAsync(string key, EmailNotificationType type, NotificationRecipient r,
        Guid? refundId, CancellationToken ct)
    {
        if (await outbox.ExistsAsync(key, ct)) return;
        var email = (r.Email ?? "").Trim();
        var valid = EmailAddressRules.IsValid(email);
        outbox.Add(new EmailOutboxMessage
        {
            EventKey = key, NotificationType = type, OrderId = r.OrderId, BookingId = r.BookingId,
            FinancialTransactionId = refundId,
            RecipientEmail = email.Length > 254 ? email[..254] : email,
            RecipientName = r.Name.Length > 200 ? r.Name[..200] : r.Name,
            // Người nhận không hợp lệ vẫn được ghi (DeadLetter) để vận hành thấy, không mất thông báo âm thầm.
            Status = valid ? EmailOutboxStatus.Pending : EmailOutboxStatus.DeadLetter,
            LastError = valid ? null : "InvalidRecipient",
            NextAttemptAt = clock.Now
        });
    }
}

public static class EmailAddressRules
{
    /// <summary>Địa chỉ trần (không display name), ≤254 ký tự, không khoảng trắng/CR/LF.</summary>
    public static bool IsValid(string? value) =>
        !string.IsNullOrEmpty(value) && value.Length <= 254 && !value.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)) &&
        MailAddress.TryCreate(value, out var parsed) && parsed.Address == value && parsed.Host.Contains('.');
}
