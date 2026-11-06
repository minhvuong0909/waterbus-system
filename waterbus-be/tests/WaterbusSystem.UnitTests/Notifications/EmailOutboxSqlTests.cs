using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Notifications;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.Infrastructure.Repositories;
using WaterbusSystem.Infrastructure.Services.Checkout;
using WaterbusSystem.UnitTests.Bookings;
using WaterbusSystem.UnitTests.Domain;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;
using static WaterbusSystem.UnitTests.Notifications.NotificationScenario;

namespace WaterbusSystem.UnitTests.Notifications;

/// <summary>Một DB SQL Server tạm cho cả lớp (các test dùng khoá/Id riêng nên độc lập). Skip khi thiếu WATERBUS_TEST_SQLSERVER.</summary>
public sealed class EmailOutboxDb : IAsyncLifetime
{
    public string? Connection { get; private set; }
    public ApplicationDbContext Context() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(Connection!).Options);

    public async Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable("WATERBUS_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(configured)) return;
        Connection = new SqlConnectionStringBuilder(configured) { InitialCatalog = "WaterbusDomainTests_Email_" + Guid.NewGuid().ToString("N") }.ConnectionString;
        await using var c = Context(); await c.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (Connection is null) return;
        if (!new SqlConnectionStringBuilder(Connection).InitialCatalog.StartsWith("WaterbusDomainTests_Email_")) throw new InvalidOperationException("Unsafe test database.");
        await using var c = Context(); await c.Database.EnsureDeletedAsync();
    }
}

[Collection(SqlCollections.Serial)]
public sealed class EmailOutboxSqlTests(EmailOutboxDb db) : IClassFixture<EmailOutboxDb>
{
    private static readonly DateTimeOffset T0 = DateTimeOffset.UtcNow;

    private static EmailOutboxMessage Msg(string? key = null, EmailOutboxStatus status = EmailOutboxStatus.Pending, DateTimeOffset? next = null) => new()
    { EventKey = key ?? "TEST:" + Guid.NewGuid().ToString("N"), NotificationType = EmailNotificationType.BookingConfirmed, OrderId = Guid.NewGuid(),
      RecipientEmail = "buyer@example.com", RecipientName = "Buyer", Status = status, NextAttemptAt = next ?? T0.AddSeconds(-1) };

    private async Task<List<Guid>> Insert(int count, string? prefix = null)
    {
        await using var c = db.Context(); var ids = new List<Guid>();
        for (var i = 0; i < count; i++) { var m = Msg((prefix ?? "BULK:" + Guid.NewGuid().ToString("N")) + (prefix is null ? "" : ":" + i)); ids.Add(m.Id); c.EmailOutbox.Add(m); }
        await c.SaveChangesAsync(); return ids;
    }

    private async Task<IReadOnlyList<EmailOutboxMessage>> Claim(Guid token, int batch = 100, DateTimeOffset? now = null, TimeSpan? lease = null, int max = 5, int finMax = 5)
    { await using var c = db.Context(); return await new EmailOutboxRepository(c).ClaimAsync(now ?? T0, batch, lease ?? TimeSpan.FromMinutes(10), max, finMax, token, default); }

    private async Task<EmailOutboxMessage> Load(Guid id) { await using var c = db.Context(); return await c.EmailOutbox.AsNoTracking().SingleAsync(m => m.Id == id); }

    [SqlServerDomainFact]
    public async Task MigrationCreatesUniqueEventKeyIndexAndDispatchIndex()
    {
        await using var c = db.Context();
        var indexes = await c.Database.SqlQueryRaw<string>("SELECT i.name + ':' + CAST(i.is_unique AS varchar(1)) AS Value FROM sys.indexes i WHERE i.object_id = OBJECT_ID('EmailOutbox') AND i.name IS NOT NULL").ToListAsync();
        indexes.Should().Contain("UX_EmailOutbox_EventKey:1").And.Contain("IX_EmailOutbox_Dispatch:0");
    }

    [SqlServerDomainFact]
    public async Task DatabaseRejectsASecondRowWithTheSameEventKey()
    {
        var key = "DUP:" + Guid.NewGuid().ToString("N");
        await using (var a = db.Context()) { a.EmailOutbox.Add(Msg(key)); await a.SaveChangesAsync(); }
        await using var b = db.Context(); b.EmailOutbox.Add(Msg(key));
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => b.SaveChangesAsync());
        (ex.InnerException as SqlException)?.Number.Should().BeOneOf(2601, 2627);
    }

    [SqlServerDomainFact]
    public async Task ConcurrentDuplicateEnqueueProducesExactlyOneLogicalNotification()
    {
        Guid orderId;
        await using (var seed = db.Context())
        {
            var o = new PurchaseOrder { PurchaserName = "Buyer", PurchaserEmail = "buyer@example.com", QuotedTotal = 1 };
            seed.PurchaseOrders.Add(o); await seed.SaveChangesAsync(); orderId = o.Id;
        }
        async Task<bool> Attempt()
        {
            await using var c = db.Context();
            var outbox = new NotificationOutbox(new EmailOutboxRepository(c), new NotificationDataRepository(c), new SystemTestClock());
            try { await outbox.EnqueueBookingConfirmedAsync(orderId, default); await c.SaveChangesAsync(); return true; }
            catch (DbUpdateException) { return false; }
        }
        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(Attempt)));
        results.Should().Contain(true);
        await using var check = db.Context();
        (await check.EmailOutbox.CountAsync(m => m.EventKey == $"BOOKING_CONFIRMED:{orderId:N}")).Should().Be(1);
    }

    [SqlServerDomainFact]
    public async Task RollbackCreatesNoNotificationAndCommitSurvivesANewContext()
    {
        var rolledBack = Msg(); var committed = Msg();
        await using (var c = db.Context())
        {
            var runner = new SqlCommercialTransactionRunner(c);
            await Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunAsync<bool>(async () =>
            { c.EmailOutbox.Add(rolledBack); await c.SaveChangesAsync(); throw new InvalidOperationException("boom"); }));
        }
        await using (var c = db.Context())
            await new SqlCommercialTransactionRunner(c).RunAsync(async () => { c.EmailOutbox.Add(committed); await c.SaveChangesAsync(); return true; });

        await using var restarted = db.Context(); // "restart": ngữ cảnh/kết nối hoàn toàn mới
        (await restarted.EmailOutbox.AnyAsync(m => m.Id == rolledBack.Id)).Should().BeFalse();
        (await restarted.EmailOutbox.SingleAsync(m => m.Id == committed.Id)).Status.Should().Be(EmailOutboxStatus.Pending);
    }

    [SqlServerDomainFact]
    public async Task ConcurrentWorkersNeverClaimTheSameMessage()
    {
        var ids = (await Insert(60)).ToHashSet();
        var claimed = new List<Guid>();
        for (var round = 0; round < 8 && claimed.Count < 60; round++)
        {
            var batches = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(() => Claim(Guid.NewGuid(), 15))));
            claimed.AddRange(batches.SelectMany(b => b).Select(m => m.Id).Where(ids.Contains));
        }
        claimed.Should().OnlyHaveUniqueItems("READPAST/UPDLOCK: mỗi bản ghi chỉ thuộc một worker");
        claimed.Should().HaveCount(60);
        await using var c = db.Context();
        (await c.EmailOutbox.Where(m => ids.Contains(m.Id)).AllAsync(m => m.Status == EmailOutboxStatus.Processing && m.AttemptCount == 1 && m.LockToken != null)).Should().BeTrue();
    }

    [SqlServerDomainFact]
    public async Task AbandonedLeaseIsReclaimedAndTheOldWorkerCanNoLongerWrite()
    {
        var id = (await Insert(1)).Single();
        var dead = Guid.NewGuid(); var alive = Guid.NewGuid();
        (await Claim(dead, 1000, T0, TimeSpan.FromMinutes(1))).Should().Contain(m => m.Id == id);

        (await Claim(Guid.NewGuid(), 1000, T0.AddSeconds(30))).Should().NotContain(m => m.Id == id, "lease còn hiệu lực");
        (await Claim(alive, 1000, T0.AddMinutes(2))).Should().Contain(m => m.Id == id && m.AttemptCount == 2);

        await using var c = db.Context(); var repo = new EmailOutboxRepository(c);
        (await repo.MarkSentAsync(id, dead, "zombie", T0, default)).Should().BeFalse();
        (await Load(id)).Status.Should().Be(EmailOutboxStatus.Processing);
        (await repo.MarkSentAsync(id, alive, "250 ok", T0.AddMinutes(2), default)).Should().BeTrue();
        var final = await Load(id); final.Status.Should().Be(EmailOutboxStatus.Sent); final.ProviderMessageId.Should().Be("250 ok"); final.LockToken.Should().BeNull();
        (await repo.MarkRetryAsync(id, alive, T0, "late", default)).Should().BeFalse("đã Sent: trạng thái cuối không bị ghi đè");
        (await Claim(Guid.NewGuid(), 1000, T0.AddDays(1))).Should().NotContain(m => m.Id == id);
    }

    [SqlServerDomainFact]
    public async Task AbandonedRecordAtMaxAttemptsIsDeadLetteredNotReclaimed()
    {
        var id = (await Insert(1)).Single();
        await Claim(Guid.NewGuid(), 1000, T0, TimeSpan.FromMinutes(1), max: 1, finMax: 1);
        (await Claim(Guid.NewGuid(), 1000, T0.AddMinutes(5), max: 1, finMax: 1)).Should().NotContain(m => m.Id == id);
        var m = await Load(id); m.Status.Should().Be(EmailOutboxStatus.DeadLetter); m.LastError.Should().Contain("max attempts");
    }

    [SqlServerDomainFact]
    public async Task RetryScheduleGatesWhenAFailedMessageBecomesClaimableAgain()
    {
        var id = (await Insert(1)).Single(); var token = Guid.NewGuid();
        await Claim(token, 1000, T0);
        await using (var c = db.Context()) (await new EmailOutboxRepository(c).MarkRetryAsync(id, token, T0.AddMinutes(5), "timeout", default)).Should().BeTrue();

        (await Load(id)).Status.Should().Be(EmailOutboxStatus.Failed);
        (await Claim(Guid.NewGuid(), 1000, T0.AddMinutes(4))).Should().NotContain(m => m.Id == id);
        (await Claim(Guid.NewGuid(), 1000, T0.AddMinutes(5))).Should().Contain(m => m.Id == id && m.AttemptCount == 2);
    }

    [SqlServerDomainFact]
    public async Task ProcessorSkipsMessagesWhoseSourceNoLongerExistsAndRecordsWhyInTheDatabase()
    {
        var id = (await Insert(1, "E2E:" + Guid.NewGuid().ToString("N"))).Single();
        var transport = new SwitchTransport { Failure = new EmailDeliveryException("smtp down", true) };
        var clock = new MutableClock { Now = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5) };

        EmailOutboxProcessor Processor(ApplicationDbContext c) => new(new EmailOutboxRepository(c), Composer(c), transport, clock,
            Options.Create(new EmailOptions { Enabled = true, FromAddress = "no-reply@x.example", MaxAttempts = 3, BatchSize = 500 }), NullLogger<EmailOutboxProcessor>.Instance);

        // Bản ghi test dùng OrderId ngẫu nhiên ⇒ composer Skip; xác nhận trạng thái cuối quan sát được từ DB.
        await using (var c = db.Context()) await Processor(c).ProcessBatchAsync(default);
        var row = await Load(id);
        row.Status.Should().Be(EmailOutboxStatus.Skipped);
        row.LastError.Should().NotBeNullOrWhiteSpace();
    }

    [SqlServerDomainFact]
    public async Task ConfirmedPaymentAndItsNotificationCommitTogetherAndAnEmailOutageNeverRevertsIt()
    {
        await using var c = db.Context(); var seed = TestDbContextFactory.SeedTrip(c); var g = new CommercialCheckoutTests.Gateway();
        var runner = new SqlCommercialTransactionRunner(c);
        var checkout = await CommercialHandlerFactory.Checkout(c, runner).CreateAsync(new("Nguyen Van A", "buyer@example.com", null, [Leg(seed, seed.Seat1)]), default);
        var payments = CommercialHandlerFactory.Payment(c, g, runner);
        await payments.CreateVnPayUrlAsync(checkout.OrderId, "127.0.0.1", checkout.AccessToken, default);
        var attempt = await c.FinancialTransactions.SingleAsync(t => t.OrderId == checkout.OrderId);

        (await payments.ConfirmIpnAsync(CommercialCheckoutTests.Ipn(attempt), default)).RspCode.Should().Be("00");
        (await payments.ConfirmIpnAsync(CommercialCheckoutTests.Ipn(attempt), default)).RspCode.Should().Be("02");

        await using var fresh = db.Context();
        (await fresh.PurchaseOrders.SingleAsync(o => o.Id == checkout.OrderId)).Status.Should().Be(OrderStatus.Confirmed);
        var row = await fresh.EmailOutbox.SingleAsync(m => m.OrderId == checkout.OrderId);
        row.EventKey.Should().Be($"BOOKING_CONFIRMED:{checkout.OrderId:N}"); row.Status.Should().Be(EmailOutboxStatus.Pending);

        // Provider sập: worker ghi Failed + lịch retry; đơn vẫn Confirmed, vé vẫn còn.
        var transport = new SwitchTransport { Failure = new EmailDeliveryException("smtp down", true) };
        var clock = new MutableClock { Now = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5) };
        EmailOutboxProcessor Processor(ApplicationDbContext x) => new(new EmailOutboxRepository(x), Composer(x), transport, clock,
            Options.Create(new EmailOptions { Enabled = true, FromAddress = "no-reply@x.example", MaxAttempts = 3, BatchSize = 500 }), NullLogger<EmailOutboxProcessor>.Instance);
        await using (var w = db.Context()) await Processor(w).ProcessBatchAsync(default);
        var failed = await Load(row.Id);
        failed.Status.Should().Be(EmailOutboxStatus.Failed); failed.AttemptCount.Should().Be(1); failed.LastError.Should().NotContain("buyer@example.com");
        await using (var x = db.Context())
        {
            (await x.PurchaseOrders.SingleAsync(o => o.Id == checkout.OrderId)).Status.Should().Be(OrderStatus.Confirmed);
            (await x.Tickets.CountAsync(t => t.Booking!.OrderId == checkout.OrderId)).Should().Be(1);
        }

        // Provider hồi phục: gửi đúng 1 email thật sự (từ dữ liệu đã commit) rồi đánh dấu Sent.
        transport.Failure = null; clock.Now += TimeSpan.FromMinutes(2);
        await using (var w = db.Context()) (await Processor(w).ProcessBatchAsync(default)).Sent.Should().BeGreaterThanOrEqualTo(1);
        (await Load(row.Id)).Status.Should().Be(EmailOutboxStatus.Sent);
        var email = transport.Sent.Single(e => e.MessageKey == row.Id);
        email.ToEmail.Should().Be("buyer@example.com"); email.Images.Should().ContainSingle(); email.Subject.Should().Contain(c.Bookings.Single(b => b.OrderId == checkout.OrderId).BookingCode);
    }


    [SqlServerDomainFact]
    public async Task FinancialMessagesKeepBeingReclaimedLongerThanOperationalOnes()
    {
        var financial = Msg(); var operational = Msg(); operational.NotificationType = EmailNotificationType.TripCancelled;
        await using (var c = db.Context()) { c.EmailOutbox.AddRange(financial, operational); await c.SaveChangesAsync(); }
        await Claim(Guid.NewGuid(), 1000, T0, TimeSpan.FromMinutes(1), max: 1, finMax: 3);

        var again = await Claim(Guid.NewGuid(), 1000, T0.AddMinutes(5), max: 1, finMax: 3);

        again.Should().Contain(m => m.Id == financial.Id && m.AttemptCount == 2);
        again.Should().NotContain(m => m.Id == operational.Id);
        (await Load(operational.Id)).Status.Should().Be(EmailOutboxStatus.DeadLetter);
    }

    private sealed class StubData : INotificationDataRepository
    {
        public Task<NotificationRecipient?> GetOrderRecipientAsync(Guid o, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<NotificationRecipient>> GetConfirmedBookingRecipientsForTripAsync(Guid t, CancellationToken ct) => throw new NotSupportedException();
        public Task<NotificationRecipient?> GetRefundRecipientAsync(Guid r, CancellationToken ct) => throw new NotSupportedException();
        public Task<BookingConfirmationData?> GetBookingConfirmationAsync(Guid o, CancellationToken ct) => Task.FromResult<BookingConfirmationData?>(
            new(o, "Buyer", "buyer@example.com", OrderStatus.Confirmed, PurchaseMode.OneWay, 15000m, "VND", [NotificationTemplateTests.Leg()]));
        public Task<TripDisruptionData?> GetTripDisruptionAsync(Guid b, CancellationToken ct) => throw new NotSupportedException();
        public Task<RefundSuccessData?> GetRefundSuccessAsync(Guid r, CancellationToken ct) => throw new NotSupportedException();
    }

    [SqlServerDomainFact]
    public async Task SlowSmtpWithTwoWorkersOnTheRealOutboxNeverSendsAMessageTwice()
    {
        await using (var purge = db.Context()) await purge.EmailOutbox.IgnoreQueryFilters().ExecuteDeleteAsync(); // chỉ test này chạy (collection tuần tự): cô lập khỏi bản ghi của test khác
        var ids = (await Insert(6, "SLOW:" + Guid.NewGuid().ToString("N"))).ToHashSet();
        var clock = new MutableClock { Now = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5) };
        var options = Options.Create(new EmailOptions { Enabled = true, FromAddress = "no-reply@x.example", LeaseMinutes = 10, BatchSize = 50 });
        var sent = new List<Guid>(); var nested = false;
        EmailOutboxProcessor Worker(ApplicationDbContext c, Func<IEmailTransport> transport) => new(new EmailOutboxRepository(c),
            new NotificationEmailComposer(new StubData(), new FixedSigner(), new FixedQr(), options), transport(), clock, options, NullLogger<EmailOutboxProcessor>.Instance);

        await using var cb = db.Context(); await using var ca = db.Context();
        var transportB = new SlowTransport(clock, sent, null, slow: false);   // worker B gửi nhanh
        var workerB = Worker(cb, () => transportB);
        // Mỗi lần gửi của worker A mất 6 phút (lease 10 phút; cả lô 36 phút) và trong lúc đó worker B chạy trên DB thật.
        var transportA = new SlowTransport(clock, sent, () =>
        {
            if (nested) return; nested = true; workerB.ProcessBatchAsync(default).GetAwaiter().GetResult(); nested = false;
        });
        await Worker(ca, () => transportA).ProcessBatchAsync(default);

        sent.Where(ids.Contains).Should().HaveCount(6).And.OnlyHaveUniqueItems("không bản ghi nào bị gửi trùng do lease hết hạn giữa lô");
        await using var check = db.Context();
        (await check.EmailOutbox.Where(m => ids.Contains(m.Id)).AllAsync(m => m.Status == EmailOutboxStatus.Sent && m.AttemptCount == 1)).Should().BeTrue();
    }

    private sealed class SlowTransport(MutableClock clock, List<Guid> sent, Action? during, bool slow = true) : IEmailTransport
    {
        public Task<EmailSendResult> SendAsync(OutboundEmail e, CancellationToken ct)
        {
            if (slow) clock.Now += TimeSpan.FromMinutes(6); during?.Invoke(); sent.Add(e.MessageKey);
            return Task.FromResult(new EmailSendResult("250 ok"));
        }
    }

    private static NotificationEmailComposer Composer(ApplicationDbContext c) => new(new NotificationDataRepository(c), new FixedSigner(), new FixedQr(),
        Options.Create(new EmailOptions { Enabled = true, FromAddress = "no-reply@x.example" }));

    private sealed class MutableClock : IDateTimeService { public DateTimeOffset Now { get; set; } }
    private sealed class FixedSigner : IBookingQrSigner
    {
        public BookingQrCredential Sign(string id, int v, Guid t, DateTimeOffset at) => new("signed:" + id, "RS256", "k", v);
        public BookingQrPublicKey GetPublicKey() => new("k", "RS256", "pem");
    }
    private sealed class FixedQr : IQrImageRenderer { public byte[] RenderPng(string payload) => [1, 2, 3]; }
    private sealed class SwitchTransport : IEmailTransport
    {
        public Exception? Failure; public readonly List<OutboundEmail> Sent = [];
        public Task<EmailSendResult> SendAsync(OutboundEmail e, CancellationToken ct) { if (Failure is not null) throw Failure; Sent.Add(e); return Task.FromResult(new EmailSendResult("250 ok")); }
    }
}
