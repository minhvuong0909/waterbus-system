using FluentAssertions;
using Xunit;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Infrastructure.Persistence;

namespace WaterbusSystem.UnitTests.Seed;

public class DemoTripBoatPlanTests
{
    private static readonly Boat[] Pair = [new() { Code = "SWB-02" }, new() { Code = "SWB-03" }];

    [Fact]
    public void AtEverySlot_TheTwoCommuterRoutesUseDifferentBoats()
    {
        for (var slot = 0; slot < 9; slot++)
        {
            var rt01 = ApplicationDbContextInitializer.CommuterBoatFor(Pair, "RT01", slot);
            var rt02 = ApplicationDbContextInitializer.CommuterBoatFor(Pair, "RT02", slot);
            rt01.Code.Should().NotBe(rt02.Code);
        }
    }

    [Fact]
    public void ABoat_AlternatesRoutes_SoItStartsWhereItsPreviousTripEnded()
    {
        // SWB-02: RT01 (Bạch Đằng → Linh Đông) rồi RT02 (Linh Đông → Bạch Đằng) rồi RT01 ...
        var routes = Enumerable.Range(0, 6)
            .Select(slot => ApplicationDbContextInitializer.CommuterBoatFor(Pair, "RT01", slot).Code == "SWB-02" ? "RT01" : "RT02")
            .ToArray();
        routes.Should().Equal("RT01", "RT02", "RT01", "RT02", "RT01", "RT02");
    }

    [Fact]
    public void WithASingleBoat_NeverThrows()
    {
        ApplicationDbContextInitializer.CommuterBoatFor([Pair[0]], "RT02", 3).Code.Should().Be("SWB-02");
    }
}
