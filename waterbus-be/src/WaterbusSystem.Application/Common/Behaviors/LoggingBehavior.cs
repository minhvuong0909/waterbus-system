using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace WaterbusSystem.Application.Common.Behaviors;

/// <summary>
/// Pipeline Behavior ghi log thời gian xử lý các Request qua MediatR, cảnh báo các truy vấn chậm > 500ms
/// </summary>
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<TRequest> _logger;

    public LoggingBehavior(ILogger<TRequest> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("[START REQUEST] {RequestName}", requestName);

        var response = await next();

        stopwatch.Stop();
        var elapsedMilliseconds = stopwatch.ElapsedMilliseconds;

        if (elapsedMilliseconds > 500)
        {
            _logger.LogWarning("[SLOW REQUEST] {RequestName} hoàn thành sau {ElapsedMilliseconds}ms!", requestName, elapsedMilliseconds);
        }
        else
        {
            _logger.LogInformation("[END REQUEST] {RequestName} hoàn thành sau {ElapsedMilliseconds}ms.", requestName, elapsedMilliseconds);
        }

        return response;
    }
}
