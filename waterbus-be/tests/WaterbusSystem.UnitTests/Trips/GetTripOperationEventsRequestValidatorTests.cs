using FluentAssertions;
using WaterbusSystem.Application.DTOs.Requests.Trips;
using WaterbusSystem.Application.Validators.Trips;
using Xunit;

namespace WaterbusSystem.UnitTests.Trips;

/// <summary>
/// GetOperationEvents bind trực tiếp GetTripOperationEventsRequest từ query qua [ServiceFilter(ValidateRequestFilter)]
/// (PR #52 review) — validator này phải thực sự chạy, không chỉ tồn tại.
/// </summary>
public class GetTripOperationEventsRequestValidatorTests
{
    private static readonly GetTripOperationEventsRequestValidator Validator = new();

    [Fact]
    public void DefaultTake_ShouldPass() =>
        Validator.Validate(new GetTripOperationEventsRequest()).IsValid.Should().BeTrue();

    [Fact]
    public void ZeroTake_ShouldFail() =>
        Validator.Validate(new GetTripOperationEventsRequest(Take: 0)).IsValid.Should().BeFalse();

    [Fact]
    public void TakeAtUpperBound_ShouldPass_ButOneMore_ShouldFail()
    {
        Validator.Validate(new GetTripOperationEventsRequest(Take: 200)).IsValid.Should().BeTrue();
        Validator.Validate(new GetTripOperationEventsRequest(Take: 201)).IsValid.Should().BeFalse();
    }
}
