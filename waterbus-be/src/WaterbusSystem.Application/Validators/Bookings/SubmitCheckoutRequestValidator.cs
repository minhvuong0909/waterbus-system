using FluentValidation;

using WaterbusSystem.Application.DTOs.Requests.Bookings;

namespace WaterbusSystem.Application.Validators.Bookings;

public sealed class SubmitCheckoutRequestValidator : AbstractValidator<SubmitCheckoutRequest>
{
    public SubmitCheckoutRequestValidator()
    {
        RuleFor(x => x.CustomerName)
            .NotEmpty()
            .MaximumLength(150);

        RuleFor(x => x.CustomerEmail)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(150);

        RuleFor(x => x.CustomerPhone)
            .Matches(@"^[0-9]{10,11}$")
            .When(x => !string.IsNullOrWhiteSpace(x.CustomerPhone));

        // Passengers có thể null/rỗng (Service dùng thông tin người mua); nhưng nếu có thì phần tử không được null
        // vì Service truy cập trực tiếp p.BookingId/p.SeatId.
        RuleForEach(x => x.Passengers)
            .NotNull().WithMessage("Hành khách không được để trống.")
            .ChildRules(p =>
        {
            p.RuleFor(x => x.BookingId).NotEmpty().WithMessage("Thiếu mã booking của hành khách.");
            p.RuleFor(x => x.SeatId).NotEmpty().WithMessage("Thiếu mã ghế của hành khách.");
            p.RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
            p.RuleFor(x => x.Email)
                .MaximumLength(150)
                .EmailAddress()
                .When(x => !string.IsNullOrEmpty(x.Email));
            p.RuleFor(x => x.Phone).MaximumLength(20);
        });
    }
}
