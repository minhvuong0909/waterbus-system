using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Requests.CheckIns;
using WaterbusSystem.Application.Features.CheckIns;
using WaterbusSystem.Application.Features.CheckIns.Services;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure.Repositories;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;

namespace WaterbusSystem.UnitTests.CheckIns;

/// <summary>
/// Test CheckInService.SetCheckInWindowAsync đối chiếu BR-BOARD-05 (cửa sổ check-in theo từng điểm dừng) và BR-CFG-01.
/// </summary>
public class CheckInServiceSetCheckInWindowTests
{
    private static CheckInService CreateService(WaterbusSystem.Infrastructure.Persistence.ApplicationDbContext context) => new(
        new CheckInRepository(context), new WaterbusSystem.Infrastructure.Persistence.EfUnitOfWork(context),
        Mock.Of<ICurrentUserService>(), Mock.Of<IDateTimeService>(), new CheckInProcessor(new CheckInRepository(context)),
        new WaterbusSystem.Infrastructure.Persistence.SqlScannerAssignmentLock(context),
        Options.Create(new NoShowFinalizationOptions()), NullLogger<CheckInService>.Instance);

    [Fact]
    public async Task SetCheckInWindowAsync_WhenValid_ShouldPersistWindowOnStopCall()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var seed = TestDbContextFactory.SeedTrip(context);
        var stopCall = seed.Trip.TripStopCalls.Single(c => c.VisitOrder == 1);
        var service = CreateService(context);

        var open = seed.Trip.DepartureTime.AddMinutes(-30);
        var close = seed.Trip.DepartureTime.AddMinutes(-5);

        var result = await service.SetCheckInWindowAsync(stopCall.Id, new SetCheckInWindowRequest(open, close), CancellationToken.None);

        result.CheckInOpenAt.Should().Be(open);
        result.CheckInCloseAt.Should().Be(close);

        var updated = context.TripStopCalls.Single(c => c.Id == stopCall.Id);
        updated.CheckInOpenAt.Should().Be(open);
        updated.CheckInCloseAt.Should().Be(close);
    }

    [Fact]
    public async Task SetCheckInWindowAsync_WhenStopCallDoesNotExist_ShouldThrowNotFoundException()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var service = CreateService(context);

        var act = () => service.SetCheckInWindowAsync(
            Guid.NewGuid(), new SetCheckInWindowRequest(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(10)),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task SetCheckInWindowAsync_WhenStopCallAlreadyDeparted_ShouldThrowConflictException()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var seed = TestDbContextFactory.SeedTrip(context);
        var stopCall = seed.Trip.TripStopCalls.Single(c => c.VisitOrder == 1);
        stopCall.Status = "Departed";
        context.SaveChanges();

        var service = CreateService(context);

        var act = () => service.SetCheckInWindowAsync(
            stopCall.Id, new SetCheckInWindowRequest(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(10)),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }
}
