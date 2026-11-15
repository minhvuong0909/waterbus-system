using Hangfire;
using Microsoft.Extensions.Options;
using WaterbusSystem.Application.Features.CheckIns;
using WaterbusSystem.Application.Features.Notifications;
using WaterbusSystem.Infrastructure.BackgroundJobs;

namespace WaterbusSystem.WebApi.BackgroundJobs;

/// <summary>
/// Map dashboard Hangfire và đăng ký các recurring job theo cấu hình.
/// </summary>
public static class BackgroundJobsApplicationBuilderExtensions
{
    public const string DashboardPath = "/hangfire";

    /// <summary>Id recurring job của Weather Guard (đã gỡ); chỉ dùng để dọn lịch còn sót ở CSDL cũ.</summary>
    internal const string LegacyWeatherGuardRecurringJobId = "weather-guard";

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

        var recurringJobs = app.Services.GetRequiredService<IRecurringJobManager>();

        // Weather Guard đã bị gỡ. Triển khai cũ còn lưu recurring job này trong Hangfire: gỡ lịch (idempotent, mỗi lần khởi động) để nó
        // không chạy tiếp. Chỉ xoá định nghĩa lịch lặp; không đụng bảng/hàng đợi Hangfire hay job khác. Job đã nằm sẵn trong hàng đợi
        // sẽ thất bại một lần vì kiểu không còn tồn tại và không được lên lịch lại.
        recurringJobs.RemoveIfExists(LegacyWeatherGuardRecurringJobId);

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

        // Email outbox: luôn đăng ký khi bật; khi tắt thì gỡ job (bản ghi vẫn nằm Pending, không bị coi là đã gửi).
        var email = app.Services.GetRequiredService<IOptions<EmailOptions>>().Value;
        if (email.Enabled)
        {
            recurringJobs.AddOrUpdate<EmailOutboxJob>(
                EmailOutboxJob.RecurringJobId,
                job => job.RunAsync(CancellationToken.None),
                email.Cron,
                new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
        }
        else
        {
            recurringJobs.RemoveIfExists(EmailOutboxJob.RecurringJobId);
        }

        var captain = app.Services.GetRequiredService<IOptions<WaterbusSystem.Application.Features.Captains.CaptainOptions>>().Value;
        if (captain.AlertsEnabled)
            recurringJobs.AddOrUpdate<WaterbusSystem.Application.Interfaces.Services.ICaptainOperationsService>(
                "captain-attendance", job => job.CheckAttendanceAsync(CancellationToken.None), captain.AlertCron,
                new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
        else recurringJobs.RemoveIfExists("captain-attendance");
        return app;
    }
}
