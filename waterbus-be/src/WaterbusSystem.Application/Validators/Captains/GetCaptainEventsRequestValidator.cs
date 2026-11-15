using FluentValidation;
using WaterbusSystem.Application.DTOs.Requests.Captains;

namespace WaterbusSystem.Application.Validators.Captains;

public sealed class GetCaptainEventsRequestValidator : AbstractValidator<GetCaptainEventsRequest>
{
    public GetCaptainEventsRequestValidator() { RuleFor(r => r.Page).GreaterThan(0); RuleFor(r => r.PageSize).InclusiveBetween(1, 100); }
}
