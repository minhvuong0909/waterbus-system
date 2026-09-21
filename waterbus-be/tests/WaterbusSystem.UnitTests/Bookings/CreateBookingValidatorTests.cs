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

    [Fact]
    public void Validate_WhenAllFieldsAreValid_ShouldNotHaveAnyErrors()
    {
        // Arrange
        var command = new CreateBookingCommand(
            TripId: Guid.NewGuid(),
            SeatIds: new List<Guid> { Guid.NewGuid(), Guid.NewGuid() },
            CustomerName: "Nguyen Van A",
            CustomerEmail: "nguyenvana@gmail.com",
            CustomerPhone: "0901234567");

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenSeatListIsEmpty_ShouldFailValidation()
    {
        // Arrange
        var command = new CreateBookingCommand(
            TripId: Guid.NewGuid(),
            SeatIds: new List<Guid>(), // Danh sách ghế rỗng
            CustomerName: "Nguyen Van A",
            CustomerEmail: "nguyenvana@gmail.com",
            CustomerPhone: "0901234567");

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "SeatIds");
    }

    [Fact]
    public void Validate_WhenEmailIsInvalid_ShouldFailValidation()
    {
        // Arrange
        var command = new CreateBookingCommand(
            TripId: Guid.NewGuid(),
            SeatIds: new List<Guid> { Guid.NewGuid() },
            CustomerName: "Nguyen Van A",
            CustomerEmail: "sai-dinh-dang-email",
            CustomerPhone: "0901234567");

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "CustomerEmail");
    }
}
