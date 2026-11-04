using FluentAssertions;
using WaterbusSystem.Application.DTOs.Requests.Trips;
using WaterbusSystem.Application.Validators.Trips;
using Xunit;

namespace WaterbusSystem.UnitTests.Trips;

/// <summary>
/// GetSeats bind trực tiếp GetTripSeatMapRequest từ query qua [ServiceFilter(ValidateRequestFilter)] (PR #52 review) —
/// validator này phải thực sự chạy, không chỉ tồn tại.
/// </summary>
public class GetTripSeatMapRequestValidatorTests
{
    private static readonly GetTripSeatMapRequestValidator Validator = new();

    [Fact]
    public void DistinctNonEmptyCallIds_ShouldPass() =>
        Validator.Validate(new GetTripSeatMapRequest(Guid.NewGuid(), Guid.NewGuid())).IsValid.Should().BeTrue();

    [Fact]
    public void EmptyBoardingCallId_ShouldFail() =>
        Validator.Validate(new GetTripSeatMapRequest(Guid.Empty, Guid.NewGuid())).IsValid.Should().BeFalse();

    [Fact]
    public void SameBoardingAndDisembarkingCallId_ShouldFail()
    {
        var callId = Guid.NewGuid();
        Validator.Validate(new GetTripSeatMapRequest(callId, callId)).IsValid.Should().BeFalse();
    }
}
