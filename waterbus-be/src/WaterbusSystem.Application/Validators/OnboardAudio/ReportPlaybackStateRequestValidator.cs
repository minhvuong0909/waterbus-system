using FluentValidation;
using WaterbusSystem.Application.DTOs.Requests.OnboardAudio;

namespace WaterbusSystem.Application.Validators.OnboardAudio;

public class ReportPlaybackStateRequestValidator : AbstractValidator<ReportPlaybackStateRequest>
{
    public ReportPlaybackStateRequestValidator()
    {
        RuleFor(x => x.State).Must(s => s is "Started" or "Completed" or "Failed")
            .WithMessage("State phải là Started, Completed hoặc Failed.");
        RuleFor(x => x.Message).MaximumLength(200);
    }
}
