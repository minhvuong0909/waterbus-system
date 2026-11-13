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
/// với cột lưu 7 chữ số thập phân (test trên bộ nhớ không chứng minh được điều này). Cần WATERBUS_TEST_SQLSERVER.
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
        await new StationService(new StationRepository(write), new EfUnitOfWork(write)).CreateAsync(At("ST-A", 10.7752, 106.7073));

        await using var read = Context();                                           // đọc lại qua kết nối khác: giá trị đã làm tròn theo cột
        var service = new StationService(new StationRepository(read), new EfUnitOfWork(read));
        var act = () => service.CreateAsync(At("ST-B", 10.7752, 106.7073));

        await act.Should().ThrowAsync<ValidationException>();
    }

    [SqlServerDomainFact]
    public async Task NearbyButDifferentPoints_AreAllowed_AndSevenDecimalsStillCount()
    {
        await using var context = Context();
        var service = new StationService(new StationRepository(context), new EfUnitOfWork(context));
        await service.CreateAsync(At("ST-A", 10.7752000, 106.7073000));

        await service.CreateAsync(At("ST-B", 10.7752001, 106.7073000));            // lệch 1e-7 (khoảng 1 cm): coi là điểm khác
        await service.CreateAsync(At("ST-C", 10.7753, 106.7073));                  // lệch khoảng 11 m
        var act = () => service.CreateAsync(At("ST-D", 10.7752, 106.7073));

        await act.Should().ThrowAsync<ValidationException>();
        (await service.GetActiveStationsAsync()).Should().HaveCount(3);
    }
}
