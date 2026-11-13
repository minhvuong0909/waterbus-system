using FluentAssertions;
using WaterbusSystem.Application.DTOs.Requests.Stations;
using WaterbusSystem.Application.Features.Stations.Services;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.Infrastructure.Repositories;
using WaterbusSystem.UnitTests.TestHelpers;
using ValidationException = WaterbusSystem.Application.Common.Exceptions.ValidationException;
using Xunit;

namespace WaterbusSystem.UnitTests.Stations;

public class StationServiceTests
{
    private static StationService CreateService(ApplicationDbContext context) =>
        new(new StationRepository(context), new EfUnitOfWork(context));

    private static CreateStationRequest NewStationRequest(string code = "ST-99") => new(
        Code: code,
        Name: "Ben Test",
        Address: "123 Duong Test",
        Latitude: 10.0,
        Longitude: 106.0,
        OrderIndex: 1);

    [Fact]
    public async Task CreateAsync_WithNewCode_CreatesStationAndReturnsId()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var service = CreateService(context);

        var id = await service.CreateAsync(NewStationRequest(), CancellationToken.None);

        var station = await service.GetByIdAsync(id, CancellationToken.None);
        station.Code.Should().Be("ST-99");
        station.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_WhenCodeAlreadyExists_ThrowsValidationException()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var service = CreateService(context);
        await service.CreateAsync(NewStationRequest("ST-DUP"), CancellationToken.None);

        var act = () => service.CreateAsync(NewStationRequest("ST-DUP"), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task UpdateAsync_WhenStationExists_UpdatesFields()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var service = CreateService(context);
        var id = await service.CreateAsync(NewStationRequest(), CancellationToken.None);

        await service.UpdateAsync(id, new UpdateStationRequest(
            Name: "Ben Da Doi Ten",
            Address: "456 Duong Moi",
            Latitude: 11.0,
            Longitude: 107.0,
            OrderIndex: 5,
            IsActive: false), CancellationToken.None);

        var station = await service.GetByIdAsync(id, CancellationToken.None);
        station.Name.Should().Be("Ben Da Doi Ten");
        station.OrderIndex.Should().Be(5);
        station.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_WhenStationDoesNotExist_ThrowsNotFoundException()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var service = CreateService(context);

        var act = () => service.UpdateAsync(Guid.NewGuid(), new UpdateStationRequest(
            "X", "Y", 1, 1, 0, true), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetByIdAsync_WhenStationDoesNotExist_ThrowsNotFoundException()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var service = CreateService(context);

        var act = () => service.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetActiveStationsAsync_OnlyReturnsActiveStationsOrderedByOrderIndex()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var service = CreateService(context);
        var secondId = await service.CreateAsync(NewStationRequest("ST-2") with { OrderIndex = 2, Latitude = 10.2 }, CancellationToken.None);
        var firstId = await service.CreateAsync(NewStationRequest("ST-1") with { OrderIndex = 1, Latitude = 10.1 }, CancellationToken.None);
        var inactiveId = await service.CreateAsync(NewStationRequest("ST-OFF") with { OrderIndex = 0, Latitude = 10.3 }, CancellationToken.None);
        await service.UpdateAsync(inactiveId, new UpdateStationRequest("Off", "", 0, 0, 0, false), CancellationToken.None);

        var stations = await service.GetActiveStationsAsync(CancellationToken.None);

        stations.Select(s => s.Id).Should().ContainInOrder(firstId, secondId);
        stations.Select(s => s.Id).Should().NotContain(inactiveId);
    }

    [Fact]
    public async Task GetPageAsync_WithIsActiveOnlyFilter_ReturnsOnlyActiveStations()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var service = CreateService(context);
        var activeId = await service.CreateAsync(NewStationRequest("ST-ACTIVE"), CancellationToken.None);
        var inactiveId = await service.CreateAsync(NewStationRequest("ST-INACTIVE") with { Latitude = 10.4 }, CancellationToken.None);
        await service.UpdateAsync(inactiveId, new UpdateStationRequest("Inactive", "", 0, 0, 0, false), CancellationToken.None);

        var page = await service.GetPageAsync(new GetStationsPagedRequest(IsActiveOnly: true), CancellationToken.None);

        page.Items.Select(s => s.Id).Should().Contain(activeId).And.NotContain(inactiveId);
    }

    [Fact]
    public async Task UpdateAsync_WhenBodyContainsMismatchedId_IdFromRouteIsAuthoritative()
    {
        // UpdateStationRequest không có field Id. Nếu client (cũ) vẫn gửi "id" lệch trong JSON body,
        // System.Text.Json bỏ qua field không khớp property nào — route id vẫn là nguồn duy nhất.
        using var context = TestDbContextFactory.CreateDbContext();
        var service = CreateService(context);
        var routeId = await service.CreateAsync(NewStationRequest("ST-ROUTE-ID"), CancellationToken.None);

        var json = $$"""
            {"id":"{{Guid.NewGuid()}}","name":"Updated","address":"456","latitude":11.0,"longitude":107.0,"orderIndex":5,"isActive":true}
            """;
        var request = System.Text.Json.JsonSerializer.Deserialize<UpdateStationRequest>(json,
            new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase })!;

        await service.UpdateAsync(routeId, request, CancellationToken.None);

        var station = await service.GetByIdAsync(routeId, CancellationToken.None);
        station.Name.Should().Be("Updated");
    }

    private static CreateStationRequest At(string code, double lat, double lng) =>
        new(Code: code, Name: "Ben " + code, Address: "Dia chi", Latitude: lat, Longitude: lng, OrderIndex: 1);

    [Fact]
    public async Task CreateAsync_AtTheSameCoordinatesAsAnotherStation_IsRejectedWithFieldErrors()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var service = CreateService(context);
        await service.CreateAsync(At("ST-A", 10.7752, 106.7073), CancellationToken.None);

        var act = () => service.CreateAsync(At("ST-B", 10.7752, 106.7073), CancellationToken.None);

        var error = (await act.Should().ThrowAsync<ValidationException>()).Which;
        error.Errors.Keys.Should().Contain(["Latitude", "Longitude"]);
    }

    [Fact]
    public async Task CreateAsync_AtDifferentCoordinates_IsAllowed_EvenWhenOnlyOneCoordinateMatches()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var service = CreateService(context);
        await service.CreateAsync(At("ST-A", 10.7752, 106.7073), CancellationToken.None);

        await service.CreateAsync(At("ST-B", 10.7752, 106.7100), CancellationToken.None);      // cùng vĩ độ, khác kinh độ
        await service.CreateAsync(At("ST-C", 10.7800, 106.7073), CancellationToken.None);      // cùng kinh độ, khác vĩ độ

        (await service.GetActiveStationsAsync()).Should().HaveCount(3);
    }

    [Fact]
    public async Task UpdateAsync_MovingOntoAnotherStation_IsRejected_ButKeepingItsOwnCoordinatesIsFine()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var service = CreateService(context);
        var a = await service.CreateAsync(At("ST-A", 10.7752, 106.7073), CancellationToken.None);
        var b = await service.CreateAsync(At("ST-B", 10.8000, 106.7200), CancellationToken.None);

        await service.UpdateAsync(b, new UpdateStationRequest("Doi ten", "Dia chi", 10.8000, 106.7200, 2, true));      // tọa độ của chính nó
        var act = () => service.UpdateAsync(b, new UpdateStationRequest("Doi ten", "Dia chi", 10.7752, 106.7073, 2, true));

        await act.Should().ThrowAsync<ValidationException>();
        (await service.GetByIdAsync(b)).Latitude.Should().Be(10.8000);                      // không bị đổi
        (await service.GetByIdAsync(a)).Latitude.Should().Be(10.7752);
    }

    [Fact]
    public async Task UpdateAsync_DeactivatingAStationThatAlreadyDuplicatesAnother_IsStillAllowed()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var service = CreateService(context);
        await service.CreateAsync(At("ST-A", 10.7752, 106.7073), CancellationToken.None);
        // Dữ liệu cũ: một bến trùng tọa độ được chèn thẳng, không qua service.
        var legacy = new WaterbusSystem.Domain.Entities.Station { Code = "ST-OLD", Name = "Thu Duc", Address = "Thu Duc", Latitude = 10.7752, Longitude = 106.7073, OrderIndex = 6, IsActive = true };
        context.Stations.Add(legacy);
        await context.SaveChangesAsync();

        await service.UpdateAsync(legacy.Id, new UpdateStationRequest("Thu Duc", "Thu Duc", 10.7752, 106.7073, 6, false));

        (await service.GetByIdAsync(legacy.Id)).IsActive.Should().BeFalse();
        var reactivate = () => service.UpdateAsync(legacy.Id, new UpdateStationRequest("Thu Duc", "Thu Duc", 10.7752, 106.7073, 6, true));
        await reactivate.Should().ThrowAsync<ValidationException>();                        // bật lại thì phải sửa tọa độ
    }

    [Fact]
    public async Task CreateAsync_IgnoresDeletedStations_WhenCheckingCoordinates()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var service = CreateService(context);
        var gone = new WaterbusSystem.Domain.Entities.Station { Code = "ST-GONE", Name = "Gone", Address = "x", Latitude = 10.5, Longitude = 106.5, OrderIndex = 1, IsActive = true, IsDeleted = true };
        context.Stations.Add(gone);
        await context.SaveChangesAsync();

        var id = await service.CreateAsync(At("ST-NEW", 10.5, 106.5), CancellationToken.None);

        id.Should().NotBeEmpty();
    }
}
