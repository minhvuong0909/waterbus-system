using FluentAssertions;
using WaterbusSystem.Application.DTOs.Requests.Trips;
using WaterbusSystem.Application.Validators.Trips;
using Xunit;

namespace WaterbusSystem.UnitTests.Trips;

/// <summary>
/// GetOperations bind trực tiếp GetTripOperationsRequest từ query qua [ServiceFilter(ValidateRequestFilter)] (PR #52 review) —
/// validator này phải thực sự chạy, không chỉ tồn tại.
/// </summary>
public class GetTripOperationsRequestValidatorTests
{
    private static readonly GetTripOperationsRequestValidator Validator = new();
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void NoFilter_ShouldPass() =>
        Validator.Validate(new GetTripOperationsRequest()).IsValid.Should().BeTrue();

    [Fact]
    public void ToBeforeFrom_ShouldFail() =>
        Validator.Validate(new GetTripOperationsRequest(Now, Now.AddHours(-1))).IsValid.Should().BeFalse();

    [Fact]
    public void RangeExactlyAtMaxDays_ShouldPass_ButOneSecondMore_ShouldFail()
    {
        var atLimit = new GetTripOperationsRequest(Now, Now.AddDays(GetTripOperationsRequestValidator.MaxRangeDays));
        var overLimit = new GetTripOperationsRequest(Now, Now.AddDays(GetTripOperationsRequestValidator.MaxRangeDays).AddSeconds(1));

        Validator.Validate(atLimit).IsValid.Should().BeTrue();
        Validator.Validate(overLimit).IsValid.Should().BeFalse();
    }
}
