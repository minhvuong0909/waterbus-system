using FluentAssertions;
using WaterbusSystem.Application.Features.Bookings.Commands.CreateBooking;
using Xunit;

namespace WaterbusSystem.UnitTests.Bookings;

/// <summary>
/// Kiểm thử các quy tắc xác thực (FluentValidation) khi tạo đơn đặt chỗ
/// </summary>
public class CreateBookingValidatorTests
{
    private readonly CreateBookingCommandValidator _validator = new();

    private static CreateBookingCommand ValidCommand(List<Guid>? seatIds = null) => new(
        TripId: Guid.NewGuid(),
        SeatIds: seatIds ?? new List<Guid> { Guid.NewGuid(), Guid.NewGuid() },
        BoardingStationId: Guid.NewGuid(),
        DisembarkingStationId: Guid.NewGuid(),
        CustomerName: "Nguyen Van A",
        CustomerEmail: "nguyenvana@gmail.com",
        CustomerPhone: "0901234567");

    [Fact]
    public void Validate_WhenAllFieldsAreValid_ShouldNotHaveAnyErrors()
    {
        var result = _validator.Validate(ValidCommand());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenSeatListIsEmpty_ShouldFailValidation()
    {
        var command = ValidCommand(new List<Guid>());

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "SeatIds");
    }

    [Fact]
    public void Validate_WhenSeatListHasDuplicateSeatId_ShouldFailValidation()
    {
        var duplicatedSeatId = Guid.NewGuid();
        var command = ValidCommand(new List<Guid> { duplicatedSeatId, duplicatedSeatId });

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "SeatIds");
    }

    [Fact]
    public void Validate_WhenEmailIsInvalid_ShouldFailValidation()
    {
        var command = ValidCommand() with { CustomerEmail = "sai-dinh-dang-email" };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "CustomerEmail");
    }

    [Fact]
    public void Validate_WhenBoardingAndDisembarkingStationAreSame_ShouldFailValidation()
    {
        var stationId = Guid.NewGuid();
        var command = ValidCommand() with { BoardingStationId = stationId, DisembarkingStationId = stationId };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "DisembarkingStationId");
    }
}
