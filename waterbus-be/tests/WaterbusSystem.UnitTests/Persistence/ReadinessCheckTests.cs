using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.UnitTests.Domain;
using WaterbusSystem.UnitTests.TestHelpers;
using WaterbusSystem.WebApi.Health;
using Xunit;

namespace WaterbusSystem.UnitTests.Persistence;

/// <summary>Audit M07: API chỉ "ready" khi kết nối được SQL và schema đã áp dụng đủ migration.</summary>
public sealed class ReadinessCheckTests
{
    private static string? ServerConnection() => Environment.GetEnvironmentVariable("WATERBUS_TEST_SQLSERVER");

    private static ApplicationDbContext SqlContext(string database) => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseSqlServer(new SqlConnectionStringBuilder(ServerConnection()!) { InitialCatalog = database, ConnectTimeout = 3 }.ConnectionString).Options);

    [Fact]
    public async Task InMemoryDatabase_ShouldBeReady()
    {
        using var context = TestDbContextFactory.CreateDbContext();

        var result = await ReadinessCheck.RunAsync(context, CancellationToken.None);

        result.IsReady.Should().BeTrue();
    }

    [SqlServerDomainFact]
    public async Task MissingDatabase_ShouldNotBeReady()
    {
        await using var context = SqlContext("WaterbusDomainTests_Ready_Missing_" + Guid.NewGuid().ToString("N"));

        var result = await ReadinessCheck.RunAsync(context, CancellationToken.None);

        result.IsReady.Should().BeFalse();
        result.Reason.Should().Contain("cơ sở dữ liệu");
    }

    [SqlServerDomainFact]
    public async Task DatabaseWithPendingMigrations_ShouldNotBeReady_ThenReadyAfterMigrating()
    {
        var name = "WaterbusDomainTests_Ready_" + Guid.NewGuid().ToString("N");
        await using var context = SqlContext(name);
        try
        {
            await context.Database.EnsureCreatedAsync(); // có bảng nhưng chưa có lịch sử migration

            var before = await ReadinessCheck.RunAsync(context, CancellationToken.None);
            before.IsReady.Should().BeFalse();
            before.Reason.Should().Contain("migration");

            await context.Database.EnsureDeletedAsync();
            await context.Database.MigrateAsync();

            var after = await ReadinessCheck.RunAsync(context, CancellationToken.None);
            after.IsReady.Should().BeTrue();
        }
        finally
        {
            if (name.StartsWith("WaterbusDomainTests_Ready_")) await context.Database.EnsureDeletedAsync();
        }
    }
}
