using FluentAssertions;
using Moq;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Fares.Queries.GetFares;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;

namespace WaterbusSystem.UnitTests.Fares;

public class GetFaresQueryHandlerTests
{
    private static GetFaresQueryHandler CreateHandler(Infrastructure.Persistence.ApplicationDbContext context)
    {
        var dateTime = new Mock<IDateTimeService>();
        dateTime.Setup(d => d.Now).Returns(DateTimeOffset.UtcNow);
        return new GetFaresQueryHandler(context, dateTime.Object);
    }

    [Fact]
    public async Task Handle_ActiveFares_ReturnsEachTripTypeAndSeatClassSortedByPrice()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        TestDbContextFactory.SeedTrip(context);

        var fares = await CreateHandler(context).Handle(new GetFaresQuery(), CancellationToken.None);

        fares.Select(f => (f.TripType, f.Price)).Should().Equal(
            ("Commuter", TestDbContextFactory.StandardCommuterFare),
            ("Commuter", 20000m),
            ("Sightseeing", TestDbContextFactory.SightseeingStandardFare));
        fares[0].SeatClassName.Should().Be("Tieu chuan");
        fares[0].Currency.Should().Be("VND");
    }

    [Fact]
    public async Task Handle_InactiveExpiredFutureOrDeletedFares_AreNotReturned()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var seed = TestDbContextFactory.SeedTrip(context);
        var now = DateTimeOffset.UtcNow;
        FareRule Fare(decimal price) => new()
        { TripType = "Sightseeing", SeatClassId = seed.PremiumClass.Id, Price = price, Currency = "VND", EffectiveFrom = now.AddDays(-1), IsActive = true };
        var inactive = Fare(1m); inactive.IsActive = false;
        var expired = Fare(2m); expired.EffectiveTo = now.AddMinutes(-1);
        var future = Fare(3m); future.EffectiveFrom = now.AddDays(1);
        var deleted = Fare(4m); deleted.IsDeleted = true;
        context.FareRules.AddRange(inactive, expired, future, deleted);
        await context.SaveChangesAsync();

        var fares = await CreateHandler(context).Handle(new GetFaresQuery(), CancellationToken.None);

        fares.Should().NotContain(f => f.Price < 10m);
        fares.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_NoFareRules_ReturnsEmptyList()
    {
        using var context = TestDbContextFactory.CreateDbContext();

        var fares = await CreateHandler(context).Handle(new GetFaresQuery(), CancellationToken.None);

        fares.Should().BeEmpty();
    }
}
