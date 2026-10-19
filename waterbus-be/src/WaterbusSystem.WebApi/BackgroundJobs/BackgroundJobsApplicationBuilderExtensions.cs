using Hangfire;
using Microsoft.Extensions.Options;
using WaterbusSystem.Application.Features.CheckIns;
using WaterbusSystem.Application.Features.Incidents;
using WaterbusSystem.Infrastructure.BackgroundJobs;

namespace WaterbusSystem.WebApi.BackgroundJobs;

/// <summary>
/// Map dashboard Hangfire và đăng ký các recurring job theo cấu hình.
/// </summary>
public static class BackgroundJobsApplicationBuilderExtensions
{
    public const string DashboardPath = "/hangfire";

    public static WebApplication UseBackgroundJobs(this WebApplication app)
    {
        var username = app.Configuration["Hangfire:Dashboard:Username"];
        var password = app.Configuration["Hangfire:Dashboard:Password"];
        var isDevelopment = app.Environment.IsDevelopment();

        if (isDevelopment || (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password)))
        {
            app.UseHangfireDashboard(DashboardPath, new DashboardOptions
            {
                Authorization = [new HangfireDashboardAuthorizationFilter(username, password, isDevelopment)],
                DashboardTitle = "Smart Waterbus Jobs"
            });
        }

        var weather = app.Services.GetRequiredService<IOptions<WeatherOptions>>().Value;
        var recurringJobs = app.Services.GetRequiredService<IRecurringJobManager>();
        if (weather.Enabled)
        {
            recurringJobs.AddOrUpdate<WeatherGuardJob>(
                WeatherGuardJob.RecurringJobId,
                job => job.RunAsync(CancellationToken.None),
                weather.CronExpression,
                new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
        }
        else
        {
            recurringJobs.RemoveIfExists(WeatherGuardJob.RecurringJobId);
        }

        var noShow = app.Services.GetRequiredService<IOptions<NoShowFinalizationOptions>>().Value;
        if (noShow.Enabled)
        {
            recurringJobs.AddOrUpdate<NoShowFinalizationJob>(
                NoShowFinalizationJob.RecurringJobId,
                job => job.RunAsync(CancellationToken.None),
                noShow.CronExpression,
                new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
        }
        else
        {
            recurringJobs.RemoveIfExists(NoShowFinalizationJob.RecurringJobId);
        }

        return app;
    }
}
