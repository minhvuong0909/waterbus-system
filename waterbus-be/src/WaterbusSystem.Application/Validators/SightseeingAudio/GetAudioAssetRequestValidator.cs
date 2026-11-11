using FluentValidation;
using WaterbusSystem.Application.DTOs.Requests.SightseeingAudio;

namespace WaterbusSystem.Application.Validators.SightseeingAudio;

public class GetAudioAssetRequestValidator : AbstractValidator<GetAudioAssetRequest>
{
    public const string AllowedPrefix = "audio-assets/";

    public GetAudioAssetRequestValidator()
    {
        RuleFor(x => x.AssetKey)
            .NotEmpty().WithMessage("Thiếu AssetKey của tệp âm thanh.")
            .MaximumLength(400).WithMessage("AssetKey không được vượt quá 400 ký tự.")
            .Must(key => !string.IsNullOrWhiteSpace(key) && key.StartsWith(AllowedPrefix, StringComparison.Ordinal) &&
                         key.EndsWith(".mp3", StringComparison.Ordinal) &&
                         !key.Contains("..", StringComparison.Ordinal))
            .WithMessage("AssetKey không hợp lệ.");
    }
}
