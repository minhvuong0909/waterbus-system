using Hangfire;
using WaterbusSystem.Application.Interfaces.Services;

namespace WaterbusSystem.Infrastructure.BackgroundJobs;

/// <summary>
/// Hangfire recurring job "weather-guard": chỉ chuyển sang IWeatherGuardService (logic nằm ở Application).
/// Không chạy chồng; không retry tự động vì lượt sau (cron) sẽ quét lại.
/// </summary>
public class WeatherGuardJob(IWeatherGuardService weatherGuard)
{
    public const string RecurringJobId = "weather-guard";

    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    [AutomaticRetry(Attempts = 0)]
    public Task RunAsync(CancellationToken cancellationToken) => weatherGuard.RunAsync(cancellationToken);
}
