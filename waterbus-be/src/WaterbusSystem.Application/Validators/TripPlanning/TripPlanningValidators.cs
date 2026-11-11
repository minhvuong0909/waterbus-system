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

public class CreateAdHocTripRequestValidator : AbstractValidator<CreateAdHocTripRequest>
{
    public CreateAdHocTripRequestValidator()
    {
        RuleFor(x => x.RouteId).NotEmpty();
        RuleFor(x => x.TripType).Must(t => Enum.TryParse<WaterbusSystem.Domain.Enums.TripType>(t, true, out var v) && Enum.IsDefined(v))
            .WithMessage("TripType không hợp lệ.");
        RuleFor(x => x.DepartureTime).NotEmpty();
        RuleFor(x => x.Stops).NotNull().Must(s => s is { Count: >= 2 and <= 50 }).WithMessage("Trip cần 2–50 điểm dừng.");
        RuleForEach(x => x.Stops).NotNull().ChildRules(s =>
        {
            s.RuleFor(p => p.RouteStopId).NotEmpty();
            s.RuleFor(p => p.ArrivalOffsetMin).GreaterThanOrEqualTo(0).When(p => p.ArrivalOffsetMin.HasValue);
            s.RuleFor(p => p.DepartureOffsetMin).GreaterThanOrEqualTo(0).When(p => p.DepartureOffsetMin.HasValue);
        });
        RuleFor(x => x.SalesCloseMinutesBeforeDeparture).InclusiveBetween(0, 1440).When(x => x.SalesCloseMinutesBeforeDeparture.HasValue);
        RuleFor(x => x.CheckInOpenMinutesBeforeDeparture).InclusiveBetween(1, 1440).When(x => x.CheckInOpenMinutesBeforeDeparture.HasValue);
    }
}
