using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Incidents;
using WaterbusSystem.Application.Features.Incidents.Services;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.Infrastructure.Repositories;
using WaterbusSystem.Infrastructure.Services;
using WaterbusSystem.UnitTests.Domain;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;

namespace WaterbusSystem.UnitTests.Incidents;

/// <summary>
/// Audit M09: hai lượt quét thời tiết chạy cùng lúc (thủ công + theo lịch) không được tạo 2 Incident Open cho cùng tuyến.
/// InMemory không chứng minh được loại trừ lẫn nhau nên test này cần SQL Server thật (WATERBUS_TEST_SQLSERVER).
/// </summary>
public sealed class WeatherGuardConcurrencySqlTests : IAsyncLifetime
{
    private string? _connection;
    private ApplicationDbContext Context() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(_connection!).Options);

    public async Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable("WATERBUS_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(configured)) return;
        _connection = new SqlConnectionStringBuilder(configured) { InitialCatalog = "WaterbusDomainTests_Weather_" + Guid.NewGuid().ToString("N") }.ConnectionString;
        await using var c = Context();
        await c.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_connection is null) return;
        if (!new SqlConnectionStringBuilder(_connection).InitialCatalog.StartsWith("WaterbusDomainTests_Weather_"))
            throw new InvalidOperationException("Unsafe test database.");
        await using var c = Context();
        await c.Database.EnsureDeletedAsync();
    }

    private static WeatherGuardService Service(ApplicationDbContext c)
    {
        // Dịch vụ thời tiết chậm để hai lượt chắc chắn chồng lên nhau
        var weather = new Mock<IWeatherProvider>();
        weather.Setup(w => w.GetCurrentAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .Returns(async () => { await Task.Delay(400); return new WeatherObservation(15, null, 800, "test", DateTimeOffset.UtcNow); });

        return new WeatherGuardService(
            new TripRepository(c), new RouteRepository(c), new IncidentRepository(c), new EfUnitOfWork(c), new SqlWeatherGuardRunLock(c),
            weather.Object, Mock.Of<IOperationsAlertPublisher>(), new DateTimeService(),
            Options.Create(new WeatherOptions { LookaheadHours = 72, WindSpeedThresholdMs = 10.6, GustThresholdMs = 17.2, AlertWindowHours = 3 }),
            NullLogger<WeatherGuardService>.Instance);
    }

    [SqlServerDomainFact]
    public async Task ConcurrentRuns_ShouldLeaveExactlyOneOpenIncidentPerRoute()
    {
        await using (var seed = Context()) TestDbContextFactory.SeedTrip(seed);

        async Task Run()
        {
            await using var c = Context();
            await Service(c).RunAsync();
        }
        await Task.WhenAll(Run(), Run());

        await using var check = Context();
        var open = await check.Incidents.Where(i => i.Status == "Open" && i.IncidentType == "Weather").ToListAsync();
        open.GroupBy(i => i.RouteId).Should().OnlyContain(g => g.Count() == 1, "mỗi tuyến chỉ có một cảnh báo thời tiết đang mở");
        open.Should().NotBeEmpty();
    }
}
