using FluentValidation;
using WaterbusSystem.Application.DTOs.Requests.Trips;

namespace WaterbusSystem.Application.Validators.Trips;

public class SearchTripsRequestValidator : AbstractValidator<SearchTripsRequest>
{
    public SearchTripsRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.TripType).IsInEnum().When(x => x.TripType.HasValue);
    }
}
