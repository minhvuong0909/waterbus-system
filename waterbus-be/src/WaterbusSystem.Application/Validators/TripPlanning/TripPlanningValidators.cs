using FluentValidation;
using WaterbusSystem.Application.DTOs.Requests.TripPlanning;

namespace WaterbusSystem.Application.Validators.TripPlanning;

public class GenerateTripsRequestValidator : AbstractValidator<GenerateTripsRequest>
{
    public GenerateTripsRequestValidator()
    {
        RuleFor(x => x.ScheduleId).NotEmpty();
        RuleFor(x => x.ToDate).GreaterThanOrEqualTo(x => x.FromDate);
        RuleFor(x => x).Must(x => x.ToDate.DayNumber - x.FromDate.DayNumber < 31)
            .WithName("range").WithMessage("Chỉ sinh tối đa 31 ngày mỗi lần.");
        RuleFor(x => x.SalesCloseMinutesBeforeDeparture).InclusiveBetween(0, 1440).When(x => x.SalesCloseMinutesBeforeDeparture.HasValue);
        RuleFor(x => x.CheckInOpenMinutesBeforeDeparture).InclusiveBetween(1, 1440).When(x => x.CheckInOpenMinutesBeforeDeparture.HasValue);
    }
}

public class AssignTripBoatRequestValidator : AbstractValidator<AssignTripBoatRequest>
{
    public AssignTripBoatRequestValidator() => RuleFor(x => x.BoatId).NotEmpty();
}
