using FluentAssertions;
using WaterbusSystem.Application.DTOs.Requests.Bookings;
using WaterbusSystem.Application.Validators.Bookings;
using Xunit;

namespace WaterbusSystem.UnitTests.Bookings;

/// <summary>
/// GUID rỗng và phần tử null trong danh sách hành khách bị chặn sớm với thông báo rõ. CheckoutService vẫn đối chiếu
/// danh sách hành khách với các ghế đang giữ, nên đây là lớp kiểm tra sớm chứ không phải lớp duy nhất.
/// </summary>
public class SubmitCheckoutRequestValidatorTests
{
    private readonly SubmitCheckoutRequestValidator _sut = new();
    private static readonly Guid Booking = Guid.NewGuid();
    private static readonly Guid Seat = Guid.NewGuid();

    private static SubmitCheckoutRequest Request(IReadOnlyList<CheckoutPassengerRequest>? passengers) =>
        new("Nguyen An", "an@example.com", "0912345678", passengers);

    private static CheckoutPassengerRequest Passenger(Guid? booking = null, Guid? seat = null) =>
        new(booking ?? Booking, seat ?? Seat, "Tran Binh", "binh@example.com", null);

    [Fact]
    public void WithoutPassengers_IsStillValid_BecauseTheServiceFallsBackToThePurchaser()
    {
        // Giữ đúng hành vi hiện tại: không bắt buộc nhập danh sách hành khách.
        _sut.Validate(Request(null)).IsValid.Should().BeTrue();
        _sut.Validate(Request([])).IsValid.Should().BeTrue();
    }

    [Fact]
    public void WithAValidPassenger_IsValid() =>
        _sut.Validate(Request([Passenger()])).IsValid.Should().BeTrue();

    [Fact]
    public void EmptyBookingId_IsRejected()
    {
        var result = _sut.Validate(Request([Passenger(booking: Guid.Empty)]));

        result.Errors.Should().Contain(e => e.PropertyName.EndsWith("BookingId"));
    }

    [Fact]
    public void EmptySeatId_IsRejected()
    {
        var result = _sut.Validate(Request([Passenger(seat: Guid.Empty)]));

        result.Errors.Should().Contain(e => e.PropertyName.EndsWith("SeatId"));
    }

    [Fact]
    public void NullPassengerElement_IsRejectedInsteadOfBeingDereferenced()
    {
        var result = _sut.Validate(Request([Passenger(), null!]));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("Passengers[1]"));
    }

    [Fact]
    public void ExistingPassengerRulesAreUnchanged()
    {
        _sut.Validate(Request([Passenger() with { Name = "" }])).IsValid.Should().BeFalse();
        _sut.Validate(Request([Passenger() with { Email = "not-an-email" }])).IsValid.Should().BeFalse();
    }
}
