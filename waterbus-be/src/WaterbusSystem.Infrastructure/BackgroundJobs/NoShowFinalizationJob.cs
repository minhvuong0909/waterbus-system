using Hangfire;
using MediatR;
using WaterbusSystem.Application.Features.CheckIns.Commands.RunNoShowFinalization;

namespace WaterbusSystem.Infrastructure.BackgroundJobs;

/// <summary>
/// Hangfire recurring job "noshow-finalization": chỉ chuyển sang RunNoShowFinalizationCommand
/// (logic nằm ở Application). Không chạy chồng; không retry tự động vì lượt sau (cron) sẽ quét lại.
/// </summary>
public class NoShowFinalizationJob
{
    public const string RecurringJobId = "noshow-finalization";

    private readonly ISender _mediator;

    public NoShowFinalizationJob(ISender mediator)
    {
        _mediator = mediator;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    [AutomaticRetry(Attempts = 0)]
    public Task RunAsync(CancellationToken cancellationToken) =>
        _mediator.Send(new RunNoShowFinalizationCommand(), cancellationToken);
}
