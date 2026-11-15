using FluentValidation;
using WaterbusSystem.Application.DTOs.Requests.Captains;

namespace WaterbusSystem.Application.Validators.Captains;

public sealed class GetCaptainsRequestValidator : AbstractValidator<GetCaptainsRequest>
{
    public GetCaptainsRequestValidator() { RuleFor(r => r.Page).GreaterThan(0); RuleFor(r => r.PageSize).InclusiveBetween(1, 100); RuleFor(r => r.Status).IsInEnum(); RuleFor(r => r.Search).MaximumLength(200); }
}
