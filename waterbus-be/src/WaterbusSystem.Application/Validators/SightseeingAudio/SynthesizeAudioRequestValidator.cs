using FluentValidation;
using WaterbusSystem.Application.DTOs.Requests.SightseeingAudio;

namespace WaterbusSystem.Application.Validators.SightseeingAudio;

public class SynthesizeAudioRequestValidator : AbstractValidator<SynthesizeAudioRequest>
{
    public SynthesizeAudioRequestValidator()
    {
        RuleFor(x => x.Text)
            .NotEmpty().WithMessage("Văn bản kịch bản không được để trống.")
            .MaximumLength(10_000).WithMessage("Kịch bản không được vượt quá 10.000 ký tự.");
    }
}
