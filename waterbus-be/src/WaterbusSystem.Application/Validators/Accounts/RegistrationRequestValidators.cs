using FluentValidation;
using WaterbusSystem.Application.DTOs.Requests.Accounts;

namespace WaterbusSystem.Application.Validators.Accounts;

/// <summary>Quy tắc dùng chung. Email tối đa 150 ký tự để khớp độ dài PurchaserEmail của đơn (nếu dài hơn thì không bao giờ liên kết được).</summary>
internal static class AccountRules
{
    public static IRuleBuilderOptions<T, string> Email<T>(this IRuleBuilder<T, string> rule) => rule
        .NotEmpty().WithMessage("Email không được để trống.")
        .MaximumLength(150).WithMessage("Email tối đa 150 ký tự.")
        .EmailAddress().WithMessage("Định dạng email không hợp lệ.");

    public static IRuleBuilderOptions<T, string> FullName<T>(this IRuleBuilder<T, string> rule) => rule
        .NotEmpty().WithMessage("Họ tên không được để trống.")
        .MaximumLength(150).WithMessage("Họ tên tối đa 150 ký tự.");
}

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email).Email();
        RuleFor(x => x.FullName).FullName();
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Mật khẩu không được để trống.")
            .MinimumLength(8).WithMessage("Mật khẩu tối thiểu 8 ký tự.")
            .MaximumLength(100).WithMessage("Mật khẩu tối đa 100 ký tự.")
            .Matches("[a-z]").WithMessage("Mật khẩu phải có chữ thường.")
            .Matches("[A-Z]").WithMessage("Mật khẩu phải có chữ hoa.")
            .Matches("[0-9]").WithMessage("Mật khẩu phải có chữ số.");
        RuleFor(x => x.PhoneNumber)
            .Matches(@"^[0-9+()\-\s]{6,20}$").When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber))
            .WithMessage("Số điện thoại không hợp lệ.");
    }
}

public class ConfirmEmailRequestValidator : AbstractValidator<ConfirmEmailRequest>
{
    public ConfirmEmailRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("Liên kết xác nhận không hợp lệ.");
        RuleFor(x => x.Token).NotEmpty().MaximumLength(1024).WithMessage("Liên kết xác nhận không hợp lệ.");
    }
}

public class ResendVerificationRequestValidator : AbstractValidator<ResendVerificationRequest>
{
    public ResendVerificationRequestValidator() => RuleFor(x => x.Email).Email();
}

public class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.FullName).FullName();
        RuleFor(x => x.PhoneNumber)
            .Matches(@"^[0-9+()\-\s]{6,20}$").When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber))
            .WithMessage("Số điện thoại không hợp lệ.");
    }
}
