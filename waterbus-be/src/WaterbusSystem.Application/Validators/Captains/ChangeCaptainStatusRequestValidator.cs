using FluentValidation;
using WaterbusSystem.Application.DTOs.Requests.Captains;

namespace WaterbusSystem.Application.Validators.Captains;

public sealed class ChangeCaptainStatusRequestValidator : AbstractValidator<ChangeCaptainStatusRequest>
{
    public ChangeCaptainStatusRequestValidator() { RuleFor(r => r.Status).IsInEnum(); RuleFor(r => r.Reason).NotEmpty().MaximumLength(1000); }
}
