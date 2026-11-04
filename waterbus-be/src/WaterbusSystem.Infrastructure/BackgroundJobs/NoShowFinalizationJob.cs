using Hangfire;
using WaterbusSystem.Application.Interfaces.Services;

namespace WaterbusSystem.Infrastructure.BackgroundJobs;

/// <summary>
/// Hangfire recurring job "noshow-finalization": chỉ chuyển sang ICheckInService.RunNoShowFinalizationAsync
/// (logic nằm ở Application). Không chạy chồng; không retry tự động vì lượt sau (cron) sẽ quét lại.
/// </summary>
public class NoShowFinalizationJob
{
    public const string RecurringJobId = "noshow-finalization";

    private readonly ICheckInService _checkInService;

    public NoShowFinalizationJob(ICheckInService checkInService)
    {
        _checkInService = checkInService;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    [AutomaticRetry(Attempts = 0)]
    public Task RunAsync(CancellationToken cancellationToken) =>
        _checkInService.RunNoShowFinalizationAsync(cancellationToken);
}
