using FluentAssertions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Tracking.Queries.GetBoatPositionHistory;
using WaterbusSystem.Application.Features.Tracking.Queries.GetLatestBoatPositions;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure.Services.Tracking;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;

namespace WaterbusSystem.UnitTests.Tracking;

public class TrackingQueriesTests
{
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

        var result = await new GetLatestBoatPositionsQueryHandler(context, cache)
            .Handle(new GetLatestBoatPositionsQuery(), CancellationToken.None);

        result.Should().HaveCount(2);
        result.Single(r => r.BoatId == seed.Boat.Id).Latitude.Should().Be(10.1m);      // từ cache
        result.Single(r => r.BoatId == seed.OtherBoat.Id).Latitude.Should().Be(3m);    // bản ghi DB mới nhất
    }

    [Fact]
    public async Task GetLatest_BoatWithoutAnyPosition_ShouldBeOmitted()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        TestDbContextFactory.SeedTrip(context);

        var result = await new GetLatestBoatPositionsQueryHandler(context, new InMemoryBoatPositionCache())
            .Handle(new GetLatestBoatPositionsQuery(), CancellationToken.None);

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

        var page = await new GetBoatPositionHistoryQueryHandler(context)
            .Handle(new GetBoatPositionHistoryQuery(seed.Boat.Id, Now.AddMinutes(-25), Now), CancellationToken.None);

        page.Items.Select(i => i.Latitude).Should().Equal(-20m, -5m);
    }

    [Fact]
    public async Task History_UnknownBoat_ShouldThrowNotFound()
    {
        using var context = TestDbContextFactory.CreateDbContext();

        var act = () => new GetBoatPositionHistoryQueryHandler(context)
            .Handle(new GetBoatPositionHistoryQuery(Guid.NewGuid(), Now.AddHours(-1), Now), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Theory]
    [InlineData(0, false)]      // To == From
    [InlineData(-60, false)]    // To trước From
    [InlineData(60 * 24 * 8, false)] // quá 7 ngày
    [InlineData(60, true)]
    public void HistoryValidator_ShouldCheckRange(int minutesAfterFrom, bool expectedValid) =>
        new GetBoatPositionHistoryQueryValidator()
            .Validate(new GetBoatPositionHistoryQuery(Guid.NewGuid(), Now, Now.AddMinutes(minutesAfterFrom)))
            .IsValid.Should().Be(expectedValid);
}
