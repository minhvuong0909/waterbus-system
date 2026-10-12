using Hangfire;
using MediatR;
using WaterbusSystem.Application.Features.Incidents.Commands.RunWeatherGuard;

namespace WaterbusSystem.Infrastructure.BackgroundJobs;

/// <summary>
/// Hangfire recurring job "weather-guard": chỉ chuyển sang RunWeatherGuardCommand (logic nằm ở Application).
/// Không chạy chồng; không retry tự động vì lượt sau (cron) sẽ quét lại.
/// </summary>
public class WeatherGuardJob
{
    public const string RecurringJobId = "weather-guard";

    private readonly ISender _mediator;

    public WeatherGuardJob(ISender mediator)
    {
        _mediator = mediator;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    [AutomaticRetry(Attempts = 0)]
    public Task RunAsync(CancellationToken cancellationToken) =>
        _mediator.Send(new RunWeatherGuardCommand(), cancellationToken);
}
