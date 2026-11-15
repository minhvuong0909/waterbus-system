using FluentAssertions;
using WaterbusSystem.Application.DTOs.Requests.Bookings;
using WaterbusSystem.Application.Validators.Bookings;
using Xunit;

namespace WaterbusSystem.UnitTests.Bookings;

public class SubmitCheckoutRequestValidatorTests
{
    private readonly SubmitCheckoutRequestValidator _validator = new();

    private static CheckoutPassengerRequest Passenger(Guid? bookingId = null, Guid? seatId = null) =>
        new(bookingId ?? Guid.NewGuid(), seatId ?? Guid.NewGuid(), "Nguyen Van A", null, null);

    private static SubmitCheckoutRequest Request(IReadOnlyList<CheckoutPassengerRequest>? passengers) =>
        new("Nguyen Van A", "a@example.com", "0901234567", passengers);

    [Fact]
    public void WithoutPassengers_ShouldPass_ServiceFallsBackToBuyerInfo()
    {
        _validator.Validate(Request(null)).IsValid.Should().BeTrue();
        _validator.Validate(Request([])).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidPassenger_ShouldPass() =>
        _validator.Validate(Request([Passenger()])).IsValid.Should().BeTrue();

    [Fact]
    public void PassengerWithEmptyBookingId_ShouldFail() =>
        _validator.Validate(Request([Passenger(bookingId: Guid.Empty)])).IsValid.Should().BeFalse();

    [Fact]
    public void PassengerWithEmptySeatId_ShouldFail() =>
        _validator.Validate(Request([Passenger(seatId: Guid.Empty)])).IsValid.Should().BeFalse();

    [Fact]
    public void NullPassengerElement_ShouldFailInsteadOfThrowing()
    {
        var result = _validator.Validate(Request([Passenger(), null!]));

        result.IsValid.Should().BeFalse();
    }
}
