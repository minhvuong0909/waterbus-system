using FluentValidation;

using WaterbusSystem.Application.DTOs.Requests.Bookings;

namespace WaterbusSystem.Application.Validators.Bookings;

public sealed class CreateCheckoutRequestValidator
    : AbstractValidator<CreateCheckoutRequest>
{
    public CreateCheckoutRequestValidator()
    {
        RuleFor(x => x.CustomerName)
            .NotEmpty().MaximumLength(150);

        RuleFor(x => x.CustomerEmail)
            .NotEmpty().EmailAddress().MaximumLength(150);

        RuleFor(x => x.CustomerPhone)
            .Matches(@"^[0-9]{10,11}$")
            .When(x => !string.IsNullOrWhiteSpace(x.CustomerPhone));

        RuleFor(x => x.Legs)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .Must(x => x is { Count: 1 or 2 });

        RuleForEach(x => x.Legs)
            .NotNull()
            .SetValidator(new CreateCheckoutLegRequestValidator());
    }
}

public sealed class CreateCheckoutLegRequestValidator
    : AbstractValidator<CreateCheckoutLegRequest>
{
    public CreateCheckoutLegRequestValidator()
    {
        RuleFor(x => x.TripId).NotEmpty();
        RuleFor(x => x.BoardingStationId).NotEmpty();
        RuleFor(x => x.DisembarkingStationId).NotEmpty();

        RuleFor(x => x.SeatIds)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(x => x.Count <= 10)
            .Must(x => x.Distinct().Count() == x.Count);

        RuleFor(x => x.Passengers)
            .Must((leg, passengers) =>
                passengers is null ||
                passengers.Count == 0 ||
                passengers.Count == (leg.SeatIds?.Count ?? 0))
            .WithMessage(
                "Số hành khách phải khớp với số ghế.");

        RuleForEach(x => x.Passengers).ChildRules(p =>
        {
            p.RuleFor(x => x.Name)
                .NotEmpty().MaximumLength(150);

            p.RuleFor(x => x.Email)
                .MaximumLength(150).EmailAddress()
                .When(x => !string.IsNullOrEmpty(x.Email));

            p.RuleFor(x => x.Phone).MaximumLength(20);
        });
    }
}
