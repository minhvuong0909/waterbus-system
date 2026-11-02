using FluentAssertions;
using WaterbusSystem.Application.DTOs.Requests.Stations;
using WaterbusSystem.Application.Features.Stations.Services;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.UnitTests.TestHelpers;
using ValidationException = WaterbusSystem.Application.Common.Exceptions.ValidationException;
using Xunit;

namespace WaterbusSystem.UnitTests.Stations;

public class StationServiceTests
{
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
        var service = new StationService(context);

        var id = await service.CreateAsync(NewStationRequest(), CancellationToken.None);

        var station = await service.GetByIdAsync(id, CancellationToken.None);
        station.Code.Should().Be("ST-99");
        station.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_WhenCodeAlreadyExists_ThrowsValidationException()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var service = new StationService(context);
        await service.CreateAsync(NewStationRequest("ST-DUP"), CancellationToken.None);

        var act = () => service.CreateAsync(NewStationRequest("ST-DUP"), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task UpdateAsync_WhenStationExists_UpdatesFields()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var service = new StationService(context);
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
        var service = new StationService(context);

        var act = () => service.UpdateAsync(Guid.NewGuid(), new UpdateStationRequest(
            "X", "Y", 1, 1, 0, true), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetByIdAsync_WhenStationDoesNotExist_ThrowsNotFoundException()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var service = new StationService(context);

        var act = () => service.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetActiveStationsAsync_OnlyReturnsActiveStationsOrderedByOrderIndex()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var service = new StationService(context);
        var secondId = await service.CreateAsync(NewStationRequest("ST-2") with { OrderIndex = 2 }, CancellationToken.None);
        var firstId = await service.CreateAsync(NewStationRequest("ST-1") with { OrderIndex = 1 }, CancellationToken.None);
        var inactiveId = await service.CreateAsync(NewStationRequest("ST-OFF") with { OrderIndex = 0 }, CancellationToken.None);
        await service.UpdateAsync(inactiveId, new UpdateStationRequest("Off", "", 0, 0, 0, false), CancellationToken.None);

        var stations = await service.GetActiveStationsAsync(CancellationToken.None);

        stations.Select(s => s.Id).Should().ContainInOrder(firstId, secondId);
        stations.Select(s => s.Id).Should().NotContain(inactiveId);
    }

    [Fact]
    public async Task GetPageAsync_WithIsActiveOnlyFilter_ReturnsOnlyActiveStations()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var service = new StationService(context);
        var activeId = await service.CreateAsync(NewStationRequest("ST-ACTIVE"), CancellationToken.None);
        var inactiveId = await service.CreateAsync(NewStationRequest("ST-INACTIVE"), CancellationToken.None);
        await service.UpdateAsync(inactiveId, new UpdateStationRequest("Inactive", "", 0, 0, 0, false), CancellationToken.None);

        var page = await service.GetPageAsync(new GetStationsPagedRequest(IsActiveOnly: true), CancellationToken.None);

        page.Items.Select(s => s.Id).Should().Contain(activeId).And.NotContain(inactiveId);
    }
}
