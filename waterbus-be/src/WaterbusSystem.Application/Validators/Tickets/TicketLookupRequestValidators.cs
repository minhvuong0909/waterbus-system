using System.Text.RegularExpressions;
using FluentValidation;
using WaterbusSystem.Application.DTOs.Requests.Tickets;

namespace WaterbusSystem.Application.Validators.Tickets;

/// <summary>Quy tắc dùng chung cho email và mã đặt chỗ của tra cứu vé.</summary>
internal static class TicketLookupRules
{
    // BookingCode = "WB" + yyyyMMddHHmmss + 6 ký tự (bảng chữ của CodeGenerator, bỏ 0/O, 1/I/L).
    private static readonly Regex BookingCodeFormat = new("^WB[0-9]{14}[A-HJKMNP-Z2-9]{6}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool IsBookingCode(string? value) =>
        value is not null && BookingCodeFormat.IsMatch(value.Trim().ToUpperInvariant());

    public static IRuleBuilderOptions<T, string> Email<T>(this IRuleBuilder<T, string> rule) => rule
        .NotEmpty().WithMessage("Email không được để trống.")
        .MaximumLength(254).WithMessage("Email không hợp lệ.")
        .EmailAddress().WithMessage("Định dạng email không hợp lệ.");

    public static IRuleBuilderOptions<T, string> BookingCode<T>(this IRuleBuilder<T, string> rule) => rule
        .NotEmpty().WithMessage("Mã đặt chỗ không được để trống.")
        .MaximumLength(50).WithMessage("Mã đặt chỗ không hợp lệ.")
        .Must(IsBookingCode).WithMessage("Mã đặt chỗ không hợp lệ.");
}

public class RequestTicketLookupOtpRequestValidator : AbstractValidator<RequestTicketLookupOtpRequest>
{
    public RequestTicketLookupOtpRequestValidator()
    {
        RuleFor(x => x.Email).Email();
        RuleFor(x => x.BookingCode).BookingCode();
    }
}

public class VerifyTicketLookupOtpRequestValidator : AbstractValidator<VerifyTicketLookupOtpRequest>
{
    public VerifyTicketLookupOtpRequestValidator()
    {
        RuleFor(x => x.Email).Email();
        RuleFor(x => x.BookingCode).BookingCode();
        RuleFor(x => x.Otp)
            .NotEmpty().WithMessage("Mã xác thực không được để trống.")
            .Matches("^[0-9]{6}$").WithMessage("Mã xác thực gồm 6 chữ số.");
    }
}
