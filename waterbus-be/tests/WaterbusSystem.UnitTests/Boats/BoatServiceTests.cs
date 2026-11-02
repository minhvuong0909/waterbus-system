using FluentAssertions;
using WaterbusSystem.Application.DTOs.Requests.Boats;
using WaterbusSystem.Application.Features.Boats.Services;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.UnitTests.TestHelpers;
using ValidationException = WaterbusSystem.Application.Common.Exceptions.ValidationException;
using Xunit;

namespace WaterbusSystem.UnitTests.Boats;

public class BoatServiceTests
{
    private static CreateBoatRequest NewBoatRequest(string code = "SWB-99") => new(
        Code: code,
        Name: "Tau Test",
        RegistrationNumber: "REG-99",
        TotalSeats: 40,
        Manufacturer: "Yamaha",
        YearBuilt: 2024,
        CaptainUserId: null);

    [Fact]
    public async Task CreateAsync_WithNewCode_CreatesBoatAndReturnsId()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var service = new BoatService(context);

        var id = await service.CreateAsync(NewBoatRequest(), CancellationToken.None);

        var boat = await service.GetByIdAsync(id, CancellationToken.None);
        boat.Code.Should().Be("SWB-99");
        boat.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_WhenCodeAlreadyExists_ThrowsValidationException()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var service = new BoatService(context);
        await service.CreateAsync(NewBoatRequest("SWB-DUP"), CancellationToken.None);

        var act = () => service.CreateAsync(NewBoatRequest("SWB-DUP"), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task UpdateAsync_WhenBoatExists_UpdatesFields()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var service = new BoatService(context);
        var id = await service.CreateAsync(NewBoatRequest(), CancellationToken.None);

        await service.UpdateAsync(id, new UpdateBoatRequest(
            Name: "Tau Da Doi Ten",
            RegistrationNumber: "REG-NEW",
            TotalSeats: 50,
            Manufacturer: "Yanmar",
            YearBuilt: 2025,
            IsActive: false,
            CaptainUserId: null), CancellationToken.None);

        var boat = await service.GetByIdAsync(id, CancellationToken.None);
        boat.Name.Should().Be("Tau Da Doi Ten");
        boat.TotalSeats.Should().Be(50);
        boat.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_WhenBoatDoesNotExist_ThrowsNotFoundException()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var service = new BoatService(context);

        var act = () => service.UpdateAsync(Guid.NewGuid(), new UpdateBoatRequest(
            "X", "Y", 10, "Z", 2024, true, null), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetByIdAsync_WhenBoatDoesNotExist_ThrowsNotFoundException()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var service = new BoatService(context);

        var act = () => service.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetPageAsync_WithIsActiveOnlyFilter_ReturnsOnlyActiveBoats()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var service = new BoatService(context);
        var activeId = await service.CreateAsync(NewBoatRequest("SWB-ACTIVE"), CancellationToken.None);
        var inactiveId = await service.CreateAsync(NewBoatRequest("SWB-INACTIVE"), CancellationToken.None);
        await service.UpdateAsync(inactiveId, new UpdateBoatRequest(
            "Inactive Boat", "REG", 10, "Mfg", 2024, false, null), CancellationToken.None);

        var page = await service.GetPageAsync(new GetBoatsRequest(IsActiveOnly: true), CancellationToken.None);

        page.Items.Select(b => b.Id).Should().Contain(activeId).And.NotContain(inactiveId);
    }
}
