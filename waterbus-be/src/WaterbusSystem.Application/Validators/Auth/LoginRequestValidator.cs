using FluentValidation;
using WaterbusSystem.Application.DTOs.Requests.Auth;

namespace WaterbusSystem.Application.Validators.Auth;

/// <summary>
/// Quy tắc xác thực dữ liệu đầu vào cho yêu cầu đăng nhập
/// </summary>
public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email không được để trống.")
            .EmailAddress().WithMessage("Định dạng email không hợp lệ.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Mật khẩu không được để trống.");
    }
}
