using FluentAssertions;
using Hangfire;
using Hangfire.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.CheckIns;
using WaterbusSystem.Application.Features.Notifications;
using WaterbusSystem.WebApi.BackgroundJobs;
using WaterbusSystem.WebApi.Controllers;
using WaterbusSystem.WebApi.Realtime;
using Xunit;

namespace WaterbusSystem.UnitTests.Incidents;

/// <summary>
/// Giám sát thời tiết đã bị gỡ khỏi phạm vi; Incident Management và SOS của thuyền trưởng được giữ nguyên.
/// Khoá lại: không còn API/event/kiểu Weather, lịch Weather Guard cũ được dọn, các job khác vẫn đăng ký.
/// </summary>
public class WeatherRemovalTests
{
    [Fact]
    public void IncidentsController_KeepsOnlyTheIncidentManagementRoutes()
    {
        var routes = typeof(IncidentsController).GetMethods()
            .Where(m => m.DeclaringType == typeof(IncidentsController))
            .SelectMany(m => m.GetCustomAttributes(typeof(HttpMethodAttribute), false).Cast<HttpMethodAttribute>()
                .Select(a => $"{a.HttpMethods.Single()} {a.Template}"))
            .ToList();

        routes.Should().BeEquivalentTo(["GET ", "GET {id:guid}", "POST {id:guid}/resolve"]);
        routes.Should().NotContain(r => r.Contains("weather", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NoWeatherTypesEventsOrPublisherMembersRemain()
    {
        var assemblies = new[]
        {
            typeof(IOperationsAlertPublisher).Assembly, typeof(WaterbusSystem.Infrastructure.DependencyInjection).Assembly,
            typeof(IncidentsController).Assembly
        };
        assemblies.SelectMany(a => a.GetTypes()).Select(t => t.FullName!)
            .Should().NotContain(n => n.Contains("Weather", StringComparison.OrdinalIgnoreCase));

        typeof(IOperationsAlertPublisher).GetMethods().Select(m => m.Name)
            .Should().BeEquivalentTo(["PublishIncidentRaisedAsync", "PublishTripStatusChangedAsync"]);
        typeof(RealtimeContract.Events).GetFields().Select(f => f.GetRawConstantValue()!.ToString()!.ToLowerInvariant())
            .Should().NotContain(v => v.Contains("weather")).And.Contain("incidentraised");
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void StartingTheHost_DropsTheLegacyWeatherGuardSchedule_AndKeepsOtherJobsPerConfiguration(bool noShow, bool email)
    {
        var manager = new Mock<IRecurringJobManager>();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Services.AddSingleton(manager.Object);
        builder.Services.AddSingleton<IOptions<NoShowFinalizationOptions>>(
            Options.Create(new NoShowFinalizationOptions { Enabled = noShow }));
        builder.Services.AddSingleton<IOptions<EmailOptions>>(Options.Create(new EmailOptions { Enabled = email, Cron = "*/5 * * * *" }));
        using var app = builder.Build();

        app.UseBackgroundJobs(); // chạy mỗi lần khởi động: idempotent

        manager.Verify(m => m.RemoveIfExists("weather-guard"), Times.Once);
        manager.Verify(m => m.AddOrUpdate(It.Is<string>(id => id.Contains("weather", StringComparison.OrdinalIgnoreCase)),
            It.IsAny<Job>(), It.IsAny<string>(), It.IsAny<RecurringJobOptions>()), Times.Never);
        manager.Verify(m => m.AddOrUpdate("noshow-finalization", It.IsAny<Job>(), It.IsAny<string>(), It.IsAny<RecurringJobOptions>()),
            noShow ? Times.Once() : Times.Never());
        manager.Verify(m => m.RemoveIfExists("noshow-finalization"), noShow ? Times.Never() : Times.Once());
        manager.Verify(m => m.AddOrUpdate("email-outbox", It.IsAny<Job>(), It.IsAny<string>(), It.IsAny<RecurringJobOptions>()),
            email ? Times.Once() : Times.Never());
        manager.Verify(m => m.RemoveIfExists("email-outbox"), email ? Times.Never() : Times.Once());
    }
}
