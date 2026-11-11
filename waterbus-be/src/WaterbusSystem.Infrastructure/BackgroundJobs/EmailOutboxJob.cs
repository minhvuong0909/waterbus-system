using Hangfire;
using WaterbusSystem.Application.Interfaces.Services;

namespace WaterbusSystem.Infrastructure.BackgroundJobs;

/// <summary>
/// Hangfire recurring job "email-outbox": quét outbox bền vững trong SQL và gửi. Không cần enqueue sau commit
/// (mất process sau commit vẫn được quét lần sau). Chạy chồng an toàn vì claim bằng lease nguyên tử;
/// không retry ở mức Hangfire vì lượt cron sau tự quét lại và retry/backoff nằm trong outbox.
/// </summary>
public class EmailOutboxJob(IEmailOutboxProcessor processor)
{
    public const string RecurringJobId = "email-outbox";

    [AutomaticRetry(Attempts = 0)]
    public Task RunAsync(CancellationToken cancellationToken) => processor.ProcessBatchAsync(cancellationToken);
}
