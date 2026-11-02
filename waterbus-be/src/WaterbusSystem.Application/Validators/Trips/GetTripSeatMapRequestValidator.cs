using FluentValidation;
using WaterbusSystem.Application.DTOs.Requests.Trips;

namespace WaterbusSystem.Application.Validators.Trips;

public class GetTripSeatMapRequestValidator : AbstractValidator<GetTripSeatMapRequest>
{
    public GetTripSeatMapRequestValidator()
    {
        RuleFor(x => x.BoardingCallId).NotEmpty();
        RuleFor(x => x.DisembarkingCallId).NotEmpty().NotEqual(x => x.BoardingCallId);
    }
}
