using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WaterbusSystem.Application.Features.Bookings.Commands.ExpireCheckoutHolds;

namespace WaterbusSystem.Infrastructure.BackgroundJobs;

/// <summary>Dọn Order hết hạn theo deadline DB, bao gồm cửa sổ PaymentPending.</summary>
public sealed class ExpiredBookingCleanupService(IServiceScopeFactory scopeFactory,
    ILogger<ExpiredBookingCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ExpireCheckoutHoldsCommand(), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Lỗi dọn checkout hết hạn."); }
            try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
