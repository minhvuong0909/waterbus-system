using FluentAssertions;
using WaterbusSystem.Application.Common.Interfaces;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using WaterbusSystem.Application.DTOs.Requests.Tracking;
using WaterbusSystem.Application.Features.SightseeingAudio;
using WaterbusSystem.Application.Features.Tracking;
using WaterbusSystem.Application.Features.Tracking.Services;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Application.Validators.Tracking;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.Infrastructure.Repositories;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure.Services.Tracking;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;

namespace WaterbusSystem.UnitTests.Tracking;

public class TrackingQueriesTests
{
    private static BoatTrackingService CreateService(ApplicationDbContext context, IBoatPositionCache cache) =>
        new(new BoatRepository(context), new BoatPositionRepository(context), new TripRepository(context),
            new EfUnitOfWork(context), cache, Mock.Of<IBoatTrackingPublisher>(), Mock.Of<ITrackingAudioTriggerEvaluator>(),
            Mock.Of<IValidator<RecordBoatPositionRequest>>(), new GetBoatPositionHistoryRequestValidator(),
            Options.Create(new TrackingOptions()), Options.Create(new SightseeingAudioOptions()),
            NullLogger<BoatTrackingService>.Instance);

    private static readonly DateTimeOffset Now = new(2026, 10, 12, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetLatest_ShouldPreferCacheAndFallBackToDatabase()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var seed = TestDbContextFactory.SeedTrip(context);
        var cache = new InMemoryBoatPositionCache();
        await cache.SetAsync(new BoatPositionMessage(seed.Boat.Id, null, 10.1m, 106.1m, 5m, Now));
        context.BoatPositionEvents.AddRange(
            new BoatPositionEvent { BoatId = seed.Boat.Id, Latitude = 1m, Longitude = 1m, ObservedAt = Now.AddMinutes(-10) },
            new BoatPositionEvent { BoatId = seed.OtherBoat.Id, Latitude = 2m, Longitude = 2m, ObservedAt = Now.AddMinutes(-5) },
            new BoatPositionEvent { BoatId = seed.OtherBoat.Id, Latitude = 3m, Longitude = 3m, ObservedAt = Now.AddMinutes(-1) });
        context.SaveChanges();

        var result = await CreateService(context, cache).GetLatestPositionsAsync();

        result.Should().HaveCount(2);
        result.Single(r => r.BoatId == seed.Boat.Id).Latitude.Should().Be(10.1m);      // từ cache
        result.Single(r => r.BoatId == seed.OtherBoat.Id).Latitude.Should().Be(3m);    // bản ghi DB mới nhất
    }

    [Fact]
    public async Task GetLatest_BoatWithoutAnyPosition_ShouldBeOmitted()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        TestDbContextFactory.SeedTrip(context);

        var result = await CreateService(context, new InMemoryBoatPositionCache()).GetLatestPositionsAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task History_ShouldReturnOnlyRangeInAscendingOrder()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var seed = TestDbContextFactory.SeedTrip(context);
        foreach (var minutes in new[] { -30, -5, -20, 10 })
        {
            context.BoatPositionEvents.Add(new BoatPositionEvent
                { BoatId = seed.Boat.Id, Latitude = minutes, Longitude = 0, ObservedAt = Now.AddMinutes(minutes) });
        }
        context.SaveChanges();

        var page = await CreateService(context, new InMemoryBoatPositionCache())
            .GetPositionHistoryAsync(new GetBoatPositionHistoryRequest(seed.Boat.Id, Now.AddMinutes(-25), Now));

        page.Items.Select(i => i.Latitude).Should().Equal(-20m, -5m);
    }

    [Fact]
    public async Task History_UnknownBoat_ShouldThrowNotFound()
    {
        using var context = TestDbContextFactory.CreateDbContext();

        var act = () => CreateService(context, new InMemoryBoatPositionCache())
            .GetPositionHistoryAsync(new GetBoatPositionHistoryRequest(Guid.NewGuid(), Now.AddHours(-1), Now));

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Theory]
    [InlineData(0, false)]      // To == From
    [InlineData(-60, false)]    // To trước From
    [InlineData(60 * 24 * 8, false)] // quá 7 ngày
    [InlineData(60, true)]
    public void HistoryValidator_ShouldCheckRange(int minutesAfterFrom, bool expectedValid) =>
        new GetBoatPositionHistoryRequestValidator()
            .Validate(new GetBoatPositionHistoryRequest(Guid.NewGuid(), Now, Now.AddMinutes(minutesAfterFrom)))
            .IsValid.Should().Be(expectedValid);
}
