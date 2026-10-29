using FluentValidation;
using WaterbusSystem.Application.DTOs.Requests.SightseeingAudio;

namespace WaterbusSystem.Application.Validators.SightseeingAudio;

public sealed class PublishAudioGuideRequestValidator : AbstractValidator<PublishAudioGuideRequest>
{
    public PublishAudioGuideRequestValidator()
    {
        RuleFor(x => x.PoiId).NotEmpty();
        RuleFor(x => x.AudioAssetUri)
            .NotEmpty()
            .MaximumLength(500)
            .Must(uri => Uri.TryCreate(uri, UriKind.Absolute, out var parsed)
                && parsed.Scheme == Uri.UriSchemeHttps
                && parsed.Query.Length == 0 && parsed.Fragment.Length == 0
                && parsed.AbsolutePath.Contains("/audio-assets/tts/", StringComparison.Ordinal)
                && parsed.AbsolutePath.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
            .WithMessage("AudioAssetUri phải là URI HTTPS của MP3 trong Blob, không có SAS token.");
        RuleFor(x => x.DurationSeconds)
            .InclusiveBetween(1, 36_000)
            .When(x => x.DurationSeconds.HasValue);
    }
}
