using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Notifications;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using Xunit;

namespace WaterbusSystem.UnitTests.Notifications;

/// <summary>Logic worker (retry/backoff/dead-letter/skip/lease) với fake in-memory. Claim nguyên tử thật được kiểm ở EmailOutboxSqlTests.</summary>
public sealed class EmailOutboxProcessorTests
{
    internal sealed class Clock : IDateTimeService { public DateTimeOffset Now { get; set; } = new(2026, 11, 6, 3, 0, 0, TimeSpan.Zero); }

    internal sealed class FakeOutbox(Clock clock) : IEmailOutboxRepository
    {
        public readonly List<EmailOutboxMessage> Items = [];
        public int ClaimCalls;
        public void Add(EmailOutboxMessage m) => Items.Add(m);
        public Task<bool> ExistsAsync(string k, CancellationToken ct) => Task.FromResult(Items.Any(m => m.EventKey == k));
        public Task<IReadOnlyList<EmailOutboxMessage>> ClaimAsync(DateTimeOffset now, int batch, TimeSpan lease, int max, Guid token, CancellationToken ct)
        {
            ClaimCalls++;
            foreach (var m in Items.Where(m => m.Status == EmailOutboxStatus.Processing && m.LockedUntil < now && m.AttemptCount >= max).ToList())
                m.Status = EmailOutboxStatus.DeadLetter;
            var due = Items.Where(m => (m.Status is EmailOutboxStatus.Pending or EmailOutboxStatus.Failed && m.NextAttemptAt <= now) ||
                (m.Status == EmailOutboxStatus.Processing && m.LockedUntil < now)).Take(batch).ToList();
            foreach (var m in due) { m.Status = EmailOutboxStatus.Processing; m.LockToken = token; m.LockedUntil = now + lease; m.AttemptCount++; }
            return Task.FromResult<IReadOnlyList<EmailOutboxMessage>>(due.Select(Copy).ToList());
        }
        private static EmailOutboxMessage Copy(EmailOutboxMessage m) => new()
        { Id = m.Id, EventKey = m.EventKey, NotificationType = m.NotificationType, OrderId = m.OrderId, BookingId = m.BookingId,
          FinancialTransactionId = m.FinancialTransactionId, RecipientEmail = m.RecipientEmail, RecipientName = m.RecipientName,
          Status = m.Status, AttemptCount = m.AttemptCount, CreatedAt = m.CreatedAt, LockToken = m.LockToken };
        private EmailOutboxMessage? Owned(Guid id, Guid token) => Items.SingleOrDefault(m => m.Id == id && m.LockToken == token && m.Status == EmailOutboxStatus.Processing);
        private Task<bool> Update(Guid id, Guid token, Action<EmailOutboxMessage> change)
        {
            var m = Owned(id, token); if (m is null) return Task.FromResult(false);
            change(m); m.LockToken = null; m.LockedUntil = null; return Task.FromResult(true);
        }
        public Task<bool> MarkSentAsync(Guid id, Guid t, string? pid, DateTimeOffset now, CancellationToken ct) => Update(id, t, m => { m.Status = EmailOutboxStatus.Sent; m.ProviderMessageId = pid; m.SentAt = now; });
        public Task<bool> MarkRetryAsync(Guid id, Guid t, DateTimeOffset next, string e, CancellationToken ct) => Update(id, t, m => { m.Status = EmailOutboxStatus.Failed; m.NextAttemptAt = next; m.LastError = e; });
        public Task<bool> MarkDeadLetterAsync(Guid id, Guid t, string e, CancellationToken ct) => Update(id, t, m => { m.Status = EmailOutboxStatus.DeadLetter; m.LastError = e; });
        public Task<bool> MarkSkippedAsync(Guid id, Guid t, string r, CancellationToken ct) => Update(id, t, m => { m.Status = EmailOutboxStatus.Skipped; m.LastError = r; });
        public Task<bool> ReleaseAsync(Guid id, Guid t, CancellationToken ct) => Update(id, t, m => { m.Status = EmailOutboxStatus.Pending; m.AttemptCount--; });
    }

    private sealed class Transport : IEmailTransport
    {
        public readonly List<OutboundEmail> Sent = [];
        public Func<OutboundEmail, Exception?> Behavior = _ => null;
        public Task<EmailSendResult> SendAsync(OutboundEmail email, CancellationToken ct)
        {
            if (Behavior(email) is { } error) throw error;
            Sent.Add(email); return Task.FromResult(new EmailSendResult("250 queued as " + Sent.Count));
        }
    }

    private sealed class Data : INotificationDataRepository
    {
        public BookingConfirmationData? Confirmation; public TripDisruptionData? Disruption; public RefundSuccessData? Refund; public Exception? Throw;
        public Task<NotificationRecipient?> GetOrderRecipientAsync(Guid o, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<NotificationRecipient>> GetConfirmedBookingRecipientsForTripAsync(Guid t, CancellationToken ct) => throw new NotSupportedException();
        public Task<NotificationRecipient?> GetRefundRecipientAsync(Guid r, CancellationToken ct) => throw new NotSupportedException();
        public Task<BookingConfirmationData?> GetBookingConfirmationAsync(Guid o, CancellationToken ct) => Throw is null ? Task.FromResult(Confirmation) : throw Throw;
        public Task<TripDisruptionData?> GetTripDisruptionAsync(Guid b, CancellationToken ct) => Task.FromResult(Disruption);
        public Task<RefundSuccessData?> GetRefundSuccessAsync(Guid r, CancellationToken ct) => Task.FromResult(Refund);
    }

    private sealed class Signer : IBookingQrSigner
    {
        public BookingQrCredential Sign(string id, int v, Guid trip, DateTimeOffset at) => new("signed:" + id, "RS256", "k1", v);
        public BookingQrPublicKey GetPublicKey() => new("k1", "RS256", "pem");
    }
    private sealed class Qr : IQrImageRenderer { public byte[] RenderPng(string payload) => [0x89, 0x50, 0x4E, 0x47]; }

    private sealed class Rig
    {
        public readonly Clock Clock = new(); public readonly FakeOutbox Outbox; public readonly Transport Transport = new(); public readonly Data Data = new();
        public EmailOptions Options = new() { Enabled = true, FromAddress = "no-reply@example.com", MaxAttempts = 3 };
        public Rig() { Outbox = new FakeOutbox(Clock); Data.Confirmation = Confirmed(); }
        public EmailOutboxProcessor Processor() => new(Outbox, new NotificationEmailComposer(Data, new Signer(), new Qr(), Options.AsOptions()),
            Transport, Clock, Options.AsOptions(), NullLogger<EmailOutboxProcessor>.Instance);
        public EmailOutboxMessage Add(EmailNotificationType type = EmailNotificationType.BookingConfirmed, Action<EmailOutboxMessage>? tweak = null)
        {
            var m = new EmailOutboxMessage { EventKey = Guid.NewGuid().ToString(), NotificationType = type, OrderId = Guid.NewGuid(), BookingId = Guid.NewGuid(),
                FinancialTransactionId = Guid.NewGuid(), RecipientEmail = "buyer@example.com", RecipientName = "Buyer", CreatedAt = Clock.Now, NextAttemptAt = Clock.Now };
            tweak?.Invoke(m); Outbox.Items.Add(m); return m;
        }
    }

    private static BookingConfirmationData Confirmed(OrderStatus status = OrderStatus.Confirmed)
    {
        var legs = new[] { NotificationTemplateTests.Leg("WB-1"), NotificationTemplateTests.Leg("WB-2") };
        return new(Guid.NewGuid(), "Buyer", "buyer@example.com", status, PurchaseMode.RoundTrip, 30000m, "VND", legs);
    }

    [Fact]
    public async Task DisabledEmailClaimsNothingAndNeverMarksAnythingSent()
    {
        var rig = new Rig { Options = new() { Enabled = false } }; var m = rig.Add();
        var result = await rig.Processor().ProcessBatchAsync(default);
        result.Should().Be(new EmailOutboxBatchResult(0, 0, 0, 0, 0));
        rig.Outbox.ClaimCalls.Should().Be(0); m.Status.Should().Be(EmailOutboxStatus.Pending); rig.Transport.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task SuccessfulDeliveryRecordsSentWithProviderIdAndAttachesOneQrPerBooking()
    {
        var rig = new Rig(); var m = rig.Add();
        var result = await rig.Processor().ProcessBatchAsync(default);

        result.Sent.Should().Be(1); m.Status.Should().Be(EmailOutboxStatus.Sent); m.ProviderMessageId.Should().Be("250 queued as 1"); m.SentAt.Should().Be(rig.Clock.Now);
        var email = rig.Transport.Sent.Single();
        email.ToEmail.Should().Be("buyer@example.com"); email.MessageKey.Should().Be(m.Id);
        email.Images.Should().HaveCount(2).And.OnlyContain(i => i.MediaType == "image/png");
        email.HtmlBody.Should().Contain(email.Images[0].ContentId).And.Contain(email.Images[1].ContentId);
        (await rig.Processor().ProcessBatchAsync(default)).Claimed.Should().Be(0, "đã Sent thì không được gửi lại");
    }

    [Fact]
    public async Task TemporaryProviderFailureRetriesWithBackoffThenSucceeds()
    {
        var rig = new Rig(); var m = rig.Add(); var fail = true;
        rig.Transport.Behavior = _ => fail ? new EmailDeliveryException("timeout", true) : null;
        var processor = rig.Processor();

        (await processor.ProcessBatchAsync(default)).Retried.Should().Be(1);
        m.Status.Should().Be(EmailOutboxStatus.Failed); m.AttemptCount.Should().Be(1);
        m.NextAttemptAt.Should().Be(rig.Clock.Now + TimeSpan.FromMinutes(1)); m.LastError.Should().Contain("EmailDeliveryException");

        (await processor.ProcessBatchAsync(default)).Claimed.Should().Be(0, "chưa đến hạn retry");
        fail = false; rig.Clock.Now += TimeSpan.FromMinutes(1);
        (await processor.ProcessBatchAsync(default)).Sent.Should().Be(1);
        m.Status.Should().Be(EmailOutboxStatus.Sent); m.AttemptCount.Should().Be(2);
    }

    [Fact]
    public async Task PermanentRejectionGoesStraightToDeadLetterWithoutRetry()
    {
        var rig = new Rig(); var m = rig.Add(); rig.Transport.Behavior = _ => new EmailDeliveryException("550 mailbox unavailable", false);
        (await rig.Processor().ProcessBatchAsync(default)).DeadLettered.Should().Be(1);
        m.Status.Should().Be(EmailOutboxStatus.DeadLetter); m.AttemptCount.Should().Be(1);
        rig.Clock.Now += TimeSpan.FromDays(1);
        (await rig.Processor().ProcessBatchAsync(default)).Claimed.Should().Be(0);
    }

    [Fact]
    public async Task ExhaustingMaxAttemptsDeadLettersInsteadOfRetryingForever()
    {
        var rig = new Rig(); var m = rig.Add(); rig.Transport.Behavior = _ => new EmailDeliveryException("down", true);
        var processor = rig.Processor();
        for (var i = 0; i < 3; i++) { await processor.ProcessBatchAsync(default); rig.Clock.Now += TimeSpan.FromHours(7); }
        m.Status.Should().Be(EmailOutboxStatus.DeadLetter); m.AttemptCount.Should().Be(3); m.LastError.Should().StartWith("MaxAttempts");
        (await processor.ProcessBatchAsync(default)).Claimed.Should().Be(0);
    }

    [Fact]
    public async Task StaleNotificationsAreSkippedNotSentAfterTheConfiguredAge()
    {
        var rig = new Rig(); var m = rig.Add(tweak: x => x.CreatedAt = rig.Clock.Now - TimeSpan.FromHours(49));
        (await rig.Processor().ProcessBatchAsync(default)).Skipped.Should().Be(1);
        m.Status.Should().Be(EmailOutboxStatus.Skipped); rig.Transport.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task ConfirmationIsSkippedWhenOrderIsNoLongerConfirmed()
    {
        var rig = new Rig(); rig.Data.Confirmation = Confirmed(OrderStatus.Expired); var m = rig.Add();
        await rig.Processor().ProcessBatchAsync(default);
        m.Status.Should().Be(EmailOutboxStatus.Skipped); rig.Transport.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task RefundEmailIsNeverSentUnlessTheRefundIsVerifiedSucceeded()
    {
        var rig = new Rig();
        var leg = NotificationTemplateTests.Leg(trip: TripStatus.Cancelled);
        rig.Data.Refund = new(Guid.NewGuid(), Guid.NewGuid(), "A", "a@example.com", FinancialTransactionStatus.Processing, 15000m, 15000m, "VND", null, null, leg, leg.Passengers);
        var pending = rig.Add(EmailNotificationType.RefundSucceeded);
        await rig.Processor().ProcessBatchAsync(default);
        pending.Status.Should().Be(EmailOutboxStatus.Skipped); rig.Transport.Sent.Should().BeEmpty();

        rig.Data.Refund = rig.Data.Refund with { Status = FinancialTransactionStatus.Succeeded, CompletedAt = rig.Clock.Now };
        var ok = rig.Add(EmailNotificationType.RefundSucceeded);
        await rig.Processor().ProcessBatchAsync(default);
        ok.Status.Should().Be(EmailOutboxStatus.Sent); rig.Transport.Sent.Single().Subject.Should().Contain("Hoàn tiền thành công");
    }

    [Theory]
    [InlineData(EmailNotificationType.TripCancelled, TripStatus.Terminated)]
    [InlineData(EmailNotificationType.TripTerminated, TripStatus.Cancelled)]
    [InlineData(EmailNotificationType.TripCancelled, TripStatus.Scheduled)]
    public async Task TripEmailsAreSkippedWhenTheTripIsNotInTheMatchingTerminalState(EmailNotificationType type, TripStatus actual)
    {
        var rig = new Rig(); rig.Data.Disruption = new(Guid.NewGuid(), "A", "a@example.com", "VND", NotificationTemplateTests.Leg(trip: actual)); var m = rig.Add(type);
        await rig.Processor().ProcessBatchAsync(default);
        m.Status.Should().Be(EmailOutboxStatus.Skipped); rig.Transport.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task WorkerThatLostItsLeaseCannotOverwriteTheNewOwnersResult()
    {
        var rig = new Rig(); var m = rig.Add();
        rig.Transport.Behavior = _ => { m.LockToken = Guid.NewGuid(); return null; }; // worker khác đã claim lại trong lúc đang gửi
        await rig.Processor().ProcessBatchAsync(default);
        m.Status.Should().Be(EmailOutboxStatus.Processing, "kết quả của worker mất lease bị bỏ qua");
        m.ProviderMessageId.Should().BeNull();
    }

    [Fact]
    public async Task AbandonedProcessingRecordIsReclaimedAfterItsLeaseExpires()
    {
        var rig = new Rig();
        var m = rig.Add(tweak: x => { x.Status = EmailOutboxStatus.Processing; x.LockToken = Guid.NewGuid(); x.LockedUntil = rig.Clock.Now.AddMinutes(10); x.AttemptCount = 1; });
        (await rig.Processor().ProcessBatchAsync(default)).Claimed.Should().Be(0, "lease còn hiệu lực");
        rig.Clock.Now += TimeSpan.FromMinutes(11);
        (await rig.Processor().ProcessBatchAsync(default)).Sent.Should().Be(1);
        m.Status.Should().Be(EmailOutboxStatus.Sent); m.AttemptCount.Should().Be(2);
    }

    [Fact]
    public async Task CancellationReleasesTheRecordAndRestoresTheAttempt()
    {
        var rig = new Rig(); var m = rig.Add(); using var cts = new CancellationTokenSource();
        rig.Transport.Behavior = _ => { cts.Cancel(); return new OperationCanceledException(cts.Token); };
        await rig.Processor().ProcessBatchAsync(cts.Token);
        m.Status.Should().Be(EmailOutboxStatus.Pending); m.AttemptCount.Should().Be(0);
    }

    [Fact]
    public async Task DiagnosticsNeverStoreTheRecipientAddressOrLineBreaks()
    {
        var rig = new Rig(); var m = rig.Add();
        rig.Transport.Behavior = _ => new InvalidOperationException("rejected buyer@example.com\r\nRCPT TO:<buyer@example.com>");
        await rig.Processor().ProcessBatchAsync(default);
        m.LastError.Should().NotContain("buyer@example.com").And.NotContain("\n").And.Contain("[email]");
    }

    [Fact]
    public async Task ComposeFailureIsRetriedAndOneBadMessageDoesNotBlockTheBatch()
    {
        var rig = new Rig(); var bad = rig.Add(); rig.Data.Throw = new TimeoutException("db");
        var good = rig.Add(EmailNotificationType.TripCancelled);
        rig.Data.Disruption = new(Guid.NewGuid(), "A", "a@example.com", "VND", NotificationTemplateTests.Leg(trip: TripStatus.Cancelled));
        var result = await rig.Processor().ProcessBatchAsync(default);
        result.Retried.Should().Be(1); result.Sent.Should().Be(1);
        bad.Status.Should().Be(EmailOutboxStatus.Failed); good.Status.Should().Be(EmailOutboxStatus.Sent);
    }

    [Theory]
    [InlineData(1, 1)] [InlineData(2, 2)] [InlineData(3, 4)] [InlineData(6, 32)] [InlineData(30, 360)]
    public void BackoffIsExponentialAndBounded(int attempt, int minutes) =>
        EmailOutboxProcessor.RetryDelay(attempt).Should().Be(TimeSpan.FromMinutes(minutes));
}

internal static class OptionsExtensions
{
    public static IOptions<EmailOptions> AsOptions(this EmailOptions o) => Options.Create(o);
}
