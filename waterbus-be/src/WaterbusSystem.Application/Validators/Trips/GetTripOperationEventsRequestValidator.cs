using FluentValidation;
using WaterbusSystem.Application.DTOs.Requests.Trips;

namespace WaterbusSystem.Application.Validators.Trips;

public class GetTripOperationEventsRequestValidator : AbstractValidator<GetTripOperationEventsRequest>
{
    public GetTripOperationEventsRequestValidator()
    {
        RuleFor(x => x.Take).InclusiveBetween(1, 200).WithMessage("Số dòng phải trong [1, 200].");
    }
}
