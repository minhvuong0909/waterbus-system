using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.DTOs.Requests.Stations;
using WaterbusSystem.Application.Features.Stations.Services;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.Infrastructure.Repositories;
using WaterbusSystem.UnitTests.Domain;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;
using ValidationException = WaterbusSystem.Application.Common.Exceptions.ValidationException;

namespace WaterbusSystem.UnitTests.Stations;

/// <summary>
/// Quy tắc "hai bến không cùng tọa độ" trên SQL Server thật: so sánh <c>ABS(cột - tham số) &lt; epsilon</c> phải dịch đúng sang SQL
/// với cột <c>float</c> (DB không làm tròn; test trên bộ nhớ không chứng minh được điều này), và các request tạo bến ĐỒNG THỜI
/// không được cùng vượt kiểm tra (khoá <see cref="SqlStationCoordinateLock"/>). Cần WATERBUS_TEST_SQLSERVER.
/// </summary>
[Collection(SqlCollections.Serial)]
public sealed class StationCoordinatesSqlTests : IAsyncLifetime
{
    private const string Prefix = "WaterbusDomainTests_StationCoords_";
    private string? _connection;

    private ApplicationDbContext Context() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(_connection!).Options);

    public async Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable("WATERBUS_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(configured)) return;
        _connection = new SqlConnectionStringBuilder(configured) { InitialCatalog = Prefix + Guid.NewGuid().ToString("N") }.ConnectionString;
        await using var context = Context();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_connection is null) return;
        if (!new SqlConnectionStringBuilder(_connection).InitialCatalog.StartsWith(Prefix)) throw new InvalidOperationException("Unsafe test database.");
        await using var context = Context();
        await context.Database.EnsureDeletedAsync();
    }

    private static CreateStationRequest At(string code, double lat, double lng) => new(code, "Ben " + code, "Dia chi", lat, lng, 1);

    [SqlServerDomainFact]
    public async Task TheSamePoint_IsRejected_AfterARoundTripThroughTheDatabase()
    {
        await using var write = Context();
        await new StationService(new StationRepository(write), new EfUnitOfWork(write), new SqlStationCoordinateLock(write)).CreateAsync(At("ST-A", 10.7752, 106.7073));

        await using var read = Context();                                           // đọc lại qua kết nối khác: giá trị lấy từ cột float của DB
        var service = new StationService(new StationRepository(read), new EfUnitOfWork(read), new SqlStationCoordinateLock(read));
        var act = () => service.CreateAsync(At("ST-B", 10.7752, 106.7073));

        await act.Should().ThrowAsync<ValidationException>();
    }

    [SqlServerDomainFact]
    public async Task NearbyButDifferentPoints_AreAllowed_AndTheEpsilonRuleHolds()
    {
        await using var context = Context();
        var service = new StationService(new StationRepository(context), new EfUnitOfWork(context), new SqlStationCoordinateLock(context));
        await service.CreateAsync(At("ST-A", 10.7752000, 106.7073000));

        await service.CreateAsync(At("ST-B", 10.7752001, 106.7073000));            // lệch 1e-7 (khoảng 1 cm): coi là điểm khác
        await service.CreateAsync(At("ST-C", 10.7753, 106.7073));                  // lệch khoảng 11 m
        var act = () => service.CreateAsync(At("ST-D", 10.7752, 106.7073));

        await act.Should().ThrowAsync<ValidationException>();
        (await service.GetActiveStationsAsync()).Should().HaveCount(3);
    }

    [SqlServerDomainFact]
    public async Task ConcurrentCreatesAtTheSameCoordinates_OnlyOneSucceeds()
    {
        const int attempts = 8;
        var start = new TaskCompletionSource();

        // Mỗi request có DbContext/connection riêng như request thật; cổng xuất phát chung để cùng lao vào bước check-then-insert.
        var tasks = Enumerable.Range(0, attempts).Select(async i =>
        {
            await using var context = Context();
            var service = new StationService(new StationRepository(context), new EfUnitOfWork(context), new SqlStationCoordinateLock(context));
            await start.Task;
            try { await service.CreateAsync(At("ST-C" + i, 10.7752, 106.7073)); return true; }
            catch (ValidationException) { return false; }
        }).ToList();
        start.SetResult();
        var outcomes = await Task.WhenAll(tasks);

        outcomes.Count(created => created).Should().Be(1);
        await using var read = Context();
        (await read.Stations.CountAsync(s => !s.IsDeleted && s.IsActive)).Should().Be(1);
    }

    [SqlServerDomainFact]
    public async Task InactiveStation_DoesNotBlockANewOne_ButReactivatingItIsRejected()
    {
        await using var context = Context();
        var service = new StationService(new StationRepository(context), new EfUnitOfWork(context), new SqlStationCoordinateLock(context));
        var a = await service.CreateAsync(At("ST-A", 10.7752, 106.7073));
        await service.UpdateAsync(a, new UpdateStationRequest("Ben A", "Dia chi", 10.7752, 106.7073, 1, false));

        await service.CreateAsync(At("ST-B", 10.7752, 106.7073));
        var reactivate = () => service.UpdateAsync(a, new UpdateStationRequest("Ben A", "Dia chi", 10.7752, 106.7073, 1, true));

        await reactivate.Should().ThrowAsync<ValidationException>();
    }
}
