using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Domain.Entities;

namespace WaterbusSystem.Application.Features.Notifications;

/// <summary>
/// Worker logic: claim (lease) → compose → send → ghi kết quả. Không phụ thuộc Hangfire/SMTP/EF.
/// Email tắt ⇒ không claim gì, không bao giờ đánh dấu Sent.
/// </summary>
public sealed partial class EmailOutboxProcessor(IEmailOutboxRepository outbox, NotificationEmailComposer composer,
    IEmailTransport transport, IDateTimeService clock, IOptions<EmailOptions> options, ILogger<EmailOutboxProcessor> logger)
    : IEmailOutboxProcessor
{
    /// <summary>Backoff luỹ thừa có chặn: 1, 2, 4, … phút, tối đa 6 giờ.</summary>
    public static TimeSpan RetryDelay(int attempt) =>
        TimeSpan.FromMinutes(Math.Min(Math.Pow(2, Math.Max(0, attempt - 1)), 360));

    public async Task<EmailOutboxBatchResult> ProcessBatchAsync(CancellationToken ct)
    {
        var o = options.Value;
        if (!o.Enabled) return new(0, 0, 0, 0, 0);

        // Claim TỪNG bản ghi một: lease (LeaseMinutes) bắt đầu từ lúc xử lý chính bản ghi đó, nên bản ghi cuối của lô
        // không thể hết lease trong lúc chờ các bản ghi trước gửi xong (tránh bị worker khác claim lại và gửi trùng).
        int claimed = 0, sent = 0, retried = 0, dead = 0, skipped = 0;
        var lease = TimeSpan.FromMinutes(Math.Max(1, o.LeaseMinutes));
        for (var i = 0; i < Math.Max(1, o.BatchSize) && !ct.IsCancellationRequested; i++)
        {
            var token = Guid.NewGuid();
            var batch = await outbox.ClaimAsync(clock.Now, 1, lease, Math.Max(1, o.MaxAttempts),
                Math.Max(1, o.FinancialMaxAttempts), token, ct);
            if (batch.Count == 0) break;
            claimed++;
            switch (await ProcessOneAsync(batch[0], token, o, ct))
            {
                case Outcome.Sent: sent++; break;
                case Outcome.Retried: retried++; break;
                case Outcome.Dead: dead++; break;
                case Outcome.Skipped: skipped++; break;
            }
        }
        return new(claimed, sent, retried, dead, skipped);
    }

    private enum Outcome { Sent, Retried, Dead, Skipped, Released }

    private async Task<Outcome> ProcessOneAsync(EmailOutboxMessage m, Guid token, EmailOptions o, CancellationToken ct)
    {
        try
        {
            // Không bao giờ tự huỷ thông báo vì tuổi: quá hạn theo chính sách loại ⇒ DeadLetter để vận hành xem xét (runbook §11).
            var reviewAfter = NotificationPolicy.ReviewAfter(m.NotificationType, o);
            if (clock.Now - m.CreatedAt > reviewAfter)
            {
                var reason = $"StaleNeedsReview: chưa gửi được sau {reviewAfter.TotalHours:0}h; cần vận hành xem xét trước khi gửi lại.";
                await outbox.MarkDeadLetterAsync(m.Id, token, reason, CancellationToken.None);
                logger.LogError("Email outbox {OutboxId} ({Type}) is stale and needs operator review.", m.Id, m.NotificationType);
                return Outcome.Dead;
            }

            var composed = await composer.ComposeAsync(m, ct);
            if (composed.Email is null) return await SkipAsync(m, token, composed.SkipReason ?? "Không còn đúng điều kiện gửi.");

            var result = await transport.SendAsync(composed.Email, ct);
            if (!await outbox.MarkSentAsync(m.Id, token, result.ProviderMessageId, clock.Now, CancellationToken.None))
                logger.LogWarning("Email outbox {OutboxId} sent but lease was lost; a duplicate delivery may occur.", m.Id);
            else
                logger.LogInformation("Email outbox {OutboxId} ({Type}) sent on attempt {Attempt}.", m.Id, m.NotificationType, m.AttemptCount);
            return Outcome.Sent;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await outbox.ReleaseAsync(m.Id, token, CancellationToken.None);
            return Outcome.Released;
        }
        catch (EmailDeliveryException ex) when (!ex.IsTransient)
        {
            await outbox.MarkDeadLetterAsync(m.Id, token, Sanitize(ex), CancellationToken.None);
            logger.LogError("Email outbox {OutboxId} ({Type}) permanently rejected: {Error}", m.Id, m.NotificationType, Sanitize(ex));
            return Outcome.Dead;
        }
        catch (Exception ex)
        {
            var error = Sanitize(ex);
            if (m.AttemptCount >= NotificationPolicy.MaxAttempts(m.NotificationType, o))
            {
                await outbox.MarkDeadLetterAsync(m.Id, token, "MaxAttempts: " + error, CancellationToken.None);
                logger.LogError("Email outbox {OutboxId} ({Type}) dead-lettered after {Attempt} attempts: {Error}", m.Id, m.NotificationType, m.AttemptCount, error);
                return Outcome.Dead;
            }
            await outbox.MarkRetryAsync(m.Id, token, clock.Now + RetryDelay(m.AttemptCount), error, CancellationToken.None);
            logger.LogWarning("Email outbox {OutboxId} ({Type}) attempt {Attempt} failed, will retry: {Error}", m.Id, m.NotificationType, m.AttemptCount, error);
            return Outcome.Retried;
        }
    }

    private async Task<Outcome> SkipAsync(EmailOutboxMessage m, Guid token, string reason)
    {
        await outbox.MarkSkippedAsync(m.Id, token, reason, CancellationToken.None);
        logger.LogInformation("Email outbox {OutboxId} ({Type}) skipped: {Reason}", m.Id, m.NotificationType, reason);
        return Outcome.Skipped;
    }

    /// <summary>Chỉ giữ loại lỗi + thông điệp ngắn; loại email/chuỗi kết nối/xuống dòng khỏi chẩn đoán lưu DB.</summary>
    public static string Sanitize(Exception ex)
    {
        var text = ex.GetType().Name + ": " + ex.Message;
        text = EmailPattern().Replace(text, "[email]");
        text = text.Replace('\r', ' ').Replace('\n', ' ');
        return text.Length > 450 ? text[..450] : text;
    }

    [GeneratedRegex(@"[^\s<>""']+@[^\s<>""']+")]
    private static partial Regex EmailPattern();
}
