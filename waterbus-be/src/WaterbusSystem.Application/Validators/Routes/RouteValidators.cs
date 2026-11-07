using FluentValidation;
using WaterbusSystem.Application.DTOs.Requests.Routes;

namespace WaterbusSystem.Application.Validators.Routes;

internal static class RouteRules
{
    public static readonly string[] ServiceTypes = ["Regular", "Sightseeing"];
}

public class RouteStopInputValidator : AbstractValidator<RouteStopInput>
{
    public RouteStopInputValidator() => RuleFor(x => x.StationId).NotEmpty();
}

public class CreateRouteRequestValidator : AbstractValidator<CreateRouteRequest>
{
    public CreateRouteRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.ServiceType).Must(s => RouteRules.ServiceTypes.Contains(s)).WithMessage("ServiceType phải là Regular hoặc Sightseeing.");
        RuleFor(x => x.EstimatedDurationMinutes).InclusiveBetween(1, 1440);
        RuleFor(x => x.DistanceKm).InclusiveBetween(0m, 1000m);
        RuleFor(x => x.Stops).NotNull().Must(s => s is { Count: >= 2 and <= 50 }).WithMessage("Route cần từ 2 đến 50 điểm dừng.");
        RuleForEach(x => x.Stops).SetValidator(new RouteStopInputValidator());
    }
}

public class UpdateRouteRequestValidator : AbstractValidator<UpdateRouteRequest>
{
    public UpdateRouteRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.ServiceType).Must(s => RouteRules.ServiceTypes.Contains(s)).WithMessage("ServiceType phải là Regular hoặc Sightseeing.");
        RuleFor(x => x.EstimatedDurationMinutes).InclusiveBetween(1, 1440);
        RuleFor(x => x.DistanceKm).InclusiveBetween(0m, 1000m);
    }
}

public class ReplaceRouteStopsRequestValidator : AbstractValidator<ReplaceRouteStopsRequest>
{
    public ReplaceRouteStopsRequestValidator()
    {
        RuleFor(x => x.Stops).NotNull().Must(s => s is { Count: >= 2 and <= 50 }).WithMessage("Route cần từ 2 đến 50 điểm dừng.");
        RuleForEach(x => x.Stops).SetValidator(new RouteStopInputValidator());
    }
}

public class GetRoutesRequestValidator : AbstractValidator<GetRoutesRequest>
{
    public GetRoutesRequestValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.ServiceType).Must(s => s is null || RouteRules.ServiceTypes.Contains(s));
    }
}
