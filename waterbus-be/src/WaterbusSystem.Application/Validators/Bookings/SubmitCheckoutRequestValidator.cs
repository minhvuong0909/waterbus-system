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

        RuleForEach(x => x.Passengers).ChildRules(p =>
        {
            p.RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
            p.RuleFor(x => x.Email)
                .MaximumLength(150)
                .EmailAddress()
                .When(x => !string.IsNullOrEmpty(x.Email));
            p.RuleFor(x => x.Phone).MaximumLength(20);
        });
    }
}
