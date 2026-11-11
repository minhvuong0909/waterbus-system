using FluentValidation;
using WaterbusSystem.Application.DTOs.Requests.SightseeingAudio;

namespace WaterbusSystem.Application.Validators.SightseeingAudio;

public sealed class GetAudioGuidePoisRequestValidator : AbstractValidator<GetAudioGuidePoisRequest>
{
    public GetAudioGuidePoisRequestValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, 1_000_000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
