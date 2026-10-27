
using FluentValidation;

namespace WaterbusSystem.Application.DTOs.Requests.Bookings;

public sealed record UpdateCheckoutSeatsRequest(
    List<Guid> SeatIds,
    IReadOnlyList<UpdateCheckoutPassengerRequest> Passengers);

public sealed record UpdateCheckoutPassengerRequest(
    string Name,
    string? Email,
    string? Phone);

public sealed class UpdateCheckoutSeatsRequestValidator
    : AbstractValidator<UpdateCheckoutSeatsRequest>
{
    public UpdateCheckoutSeatsRequestValidator()
    {
        RuleFor(x => x.SeatIds)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(ids => ids.Count <= 10)
            .Must(ids => ids.Distinct().Count() == ids.Count);

        RuleFor(x => x.Passengers)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .Must((request, passengers) =>
                passengers.Count == request.SeatIds?.Count);

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
