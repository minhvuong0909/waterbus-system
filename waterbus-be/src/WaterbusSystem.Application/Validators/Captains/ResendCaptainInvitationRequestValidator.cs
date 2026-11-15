using FluentValidation;
using WaterbusSystem.Application.DTOs.Requests.Captains;

namespace WaterbusSystem.Application.Validators.Captains;

public sealed class ResendCaptainInvitationRequestValidator : AbstractValidator<ResendCaptainInvitationRequest>
{
    public ResendCaptainInvitationRequestValidator() => RuleFor(r => r.Reason).NotEmpty().MaximumLength(1000);
}
