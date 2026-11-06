using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Infrastructure.Persistence;

namespace WaterbusSystem.Infrastructure.Repositories;

public sealed class EmailOutboxRepository(ApplicationDbContext context) : IEmailOutboxRepository
{
    public void Add(EmailOutboxMessage message) => context.EmailOutbox.Add(message);

    public Task<bool> ExistsAsync(string eventKey, CancellationToken ct) =>
        context.EmailOutbox.IgnoreQueryFilters().AsNoTracking().AnyAsync(m => m.EventKey == eventKey, ct);

    public async Task<IReadOnlyList<EmailOutboxMessage>> ClaimAsync(DateTimeOffset now, int batchSize, TimeSpan lease,
        int maxAttempts, Guid lockToken, CancellationToken ct)
    {
        // READPAST + UPDLOCK: nhiều worker/instance cùng chạy sẽ nhận các tập bản ghi rời nhau, không chờ nhau.
        // Câu 1: bản ghi bị bỏ rơi (worker chết) đã hết lượt thử ⇒ DeadLetter thay vì lặp vô hạn.
        // Câu 2: claim các bản ghi đến hạn, tăng AttemptCount ngay lúc claim (crash cũng được tính).
        const string sql = """
            UPDATE [EmailOutbox] SET [Status] = 'DeadLetter', [LockToken] = NULL, [LockedUntil] = NULL,
                [LastError] = LEFT(N'Lease expired after max attempts. ' + COALESCE([LastError], N''), 500), [UpdatedAt] = @now
            WHERE [IsDeleted] = 0 AND [Status] = 'Processing' AND [LockedUntil] < @now AND [AttemptCount] >= @maxAttempts;

            WITH due AS (
                SELECT TOP (@batch) * FROM [EmailOutbox] WITH (UPDLOCK, READPAST, ROWLOCK)
                WHERE [IsDeleted] = 0 AND (
                    ([Status] IN ('Pending', 'Failed') AND [NextAttemptAt] <= @now) OR
                    ([Status] = 'Processing' AND [LockedUntil] < @now))
                ORDER BY [NextAttemptAt])
            UPDATE due SET [Status] = 'Processing', [LockToken] = @token, [LockedUntil] = @until,
                [AttemptCount] = [AttemptCount] + 1, [UpdatedAt] = @now;
            """;
        await context.Database.ExecuteSqlRawAsync(sql, [
            new SqlParameter("@now", SqlDbType.DateTimeOffset) { Value = now },
            new SqlParameter("@until", SqlDbType.DateTimeOffset) { Value = now + lease },
            new SqlParameter("@batch", SqlDbType.Int) { Value = batchSize },
            new SqlParameter("@maxAttempts", SqlDbType.Int) { Value = maxAttempts },
            new SqlParameter("@token", SqlDbType.UniqueIdentifier) { Value = lockToken }], ct);

        return await context.EmailOutbox.AsNoTracking().Where(m => m.LockToken == lockToken && m.Status == EmailOutboxStatus.Processing)
            .OrderBy(m => m.CreatedAt).ToListAsync(ct);
    }

    private IQueryable<EmailOutboxMessage> Owned(Guid id, Guid token) =>
        context.EmailOutbox.Where(m => m.Id == id && m.LockToken == token && m.Status == EmailOutboxStatus.Processing);

    public async Task<bool> MarkSentAsync(Guid id, Guid lockToken, string? providerMessageId, DateTimeOffset now, CancellationToken ct) =>
        await Owned(id, lockToken).ExecuteUpdateAsync(s => s
            .SetProperty(m => m.Status, EmailOutboxStatus.Sent).SetProperty(m => m.SentAt, now)
            .SetProperty(m => m.ProviderMessageId, providerMessageId == null ? null : providerMessageId.Length > 200 ? providerMessageId.Substring(0, 200) : providerMessageId)
            .SetProperty(m => m.LockToken, (Guid?)null).SetProperty(m => m.LockedUntil, (DateTimeOffset?)null)
            .SetProperty(m => m.LastError, (string?)null).SetProperty(m => m.UpdatedAt, now), ct) == 1;

    public async Task<bool> MarkRetryAsync(Guid id, Guid lockToken, DateTimeOffset nextAttemptAt, string error, CancellationToken ct) =>
        await Owned(id, lockToken).ExecuteUpdateAsync(s => s
            .SetProperty(m => m.Status, EmailOutboxStatus.Failed).SetProperty(m => m.NextAttemptAt, nextAttemptAt)
            .SetProperty(m => m.LastError, Trim(error)).SetProperty(m => m.LockToken, (Guid?)null)
            .SetProperty(m => m.LockedUntil, (DateTimeOffset?)null).SetProperty(m => m.UpdatedAt, DateTimeOffset.UtcNow), ct) == 1;

    public async Task<bool> MarkDeadLetterAsync(Guid id, Guid lockToken, string error, CancellationToken ct) =>
        await Owned(id, lockToken).ExecuteUpdateAsync(s => s
            .SetProperty(m => m.Status, EmailOutboxStatus.DeadLetter).SetProperty(m => m.LastError, Trim(error))
            .SetProperty(m => m.LockToken, (Guid?)null).SetProperty(m => m.LockedUntil, (DateTimeOffset?)null)
            .SetProperty(m => m.UpdatedAt, DateTimeOffset.UtcNow), ct) == 1;

    public async Task<bool> MarkSkippedAsync(Guid id, Guid lockToken, string reason, CancellationToken ct) =>
        await Owned(id, lockToken).ExecuteUpdateAsync(s => s
            .SetProperty(m => m.Status, EmailOutboxStatus.Skipped).SetProperty(m => m.LastError, Trim(reason))
            .SetProperty(m => m.LockToken, (Guid?)null).SetProperty(m => m.LockedUntil, (DateTimeOffset?)null)
            .SetProperty(m => m.UpdatedAt, DateTimeOffset.UtcNow), ct) == 1;

    public async Task<bool> ReleaseAsync(Guid id, Guid lockToken, CancellationToken ct) =>
        await Owned(id, lockToken).ExecuteUpdateAsync(s => s
            .SetProperty(m => m.Status, EmailOutboxStatus.Pending).SetProperty(m => m.AttemptCount, m => m.AttemptCount - 1)
            .SetProperty(m => m.LockToken, (Guid?)null).SetProperty(m => m.LockedUntil, (DateTimeOffset?)null)
            .SetProperty(m => m.UpdatedAt, DateTimeOffset.UtcNow), ct) == 1;

    private static string Trim(string value) => value.Length > 500 ? value[..500] : value;
}
