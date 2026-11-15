using FluentValidation;
using WaterbusSystem.Application.DTOs.Requests.Captains;

namespace WaterbusSystem.Application.Validators.Captains;

public sealed class AssignCaptainRequestValidator : AbstractValidator<AssignCaptainRequest>
{
    public AssignCaptainRequestValidator() { RuleFor(r => r.CaptainId).NotEqual(Guid.Empty); RuleFor(r => r.Reason).NotEmpty().MaximumLength(1000); }
}
