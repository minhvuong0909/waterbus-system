using FluentAssertions;
using WaterbusSystem.Application.DTOs.Requests.Auth;
using WaterbusSystem.Application.Validators.Auth;
using Xunit;

namespace WaterbusSystem.UnitTests.Auth;

/// <summary>
/// Kiểm thử các quy tắc xác thực (FluentValidation) cho yêu cầu đăng nhập
/// </summary>
public class LoginRequestValidatorTests
{
    private readonly LoginRequestValidator _validator = new();

    [Fact]
    public void Validate_WhenEmailAndPasswordAreValid_ShouldNotHaveAnyErrors()
    {
        var request = new LoginRequest("staff@waterbus.vn", "Secret123");

        var result = _validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenEmailIsNotAValidFormat_ShouldFailValidation()
    {
        var request = new LoginRequest("not-an-email", "Secret123");

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [Fact]
    public void Validate_WhenPasswordIsEmpty_ShouldFailValidation()
    {
        var request = new LoginRequest("staff@waterbus.vn", "");

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Password");
    }
}
