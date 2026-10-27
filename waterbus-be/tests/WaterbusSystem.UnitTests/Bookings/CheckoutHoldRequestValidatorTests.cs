using WaterbusSystem.Application.DTOs.Requests.Bookings;
using Xunit;

namespace WaterbusSystem.UnitTests.Bookings;

public sealed class CheckoutHoldRequestValidatorTests
{
    private readonly CreateCheckoutHoldRequestValidator _validator = new();

    private static CheckoutHoldLegRequest ValidLeg() => new(
        Guid.NewGuid(),
        [Guid.NewGuid()],
        Guid.NewGuid(),
        Guid.NewGuid());

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void AcceptsOneOrTwoLegs(int count)
    {
        var legs = Enumerable.Range(0, count)
            .Select(_ => ValidLeg())
            .ToList();

        var result = _validator.Validate(
            new CreateCheckoutHoldRequest(legs));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void RejectsEmptyLegs()
    {
        var result = _validator.Validate(
            new CreateCheckoutHoldRequest([]));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void RejectsDuplicateSeats()
    {
        var leg = ValidLeg();
        var seatId = leg.SeatIds[0];

        var result = _validator.Validate(
            new CreateCheckoutHoldRequest([
                leg with { SeatIds = [seatId, seatId] }
            ]));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void RejectsEmptyTripId()
    {
        var result = _validator.Validate(
            new CreateCheckoutHoldRequest([
                ValidLeg() with { TripId = Guid.Empty }
            ]));

        Assert.False(result.IsValid);
    }
}
