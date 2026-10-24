using FluentValidation;
using MediatR;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Domain.Exceptions;

namespace WaterbusSystem.Application.Features.SightseeingAudio.Queries.GetAudioAsset;

/// <summary>
/// Đọc lại MP3 đã sinh (theo AssetKey trả về từ <c>POST /texttospeech/generate</c>) để Admin nghe thử.
/// Container Blob là private nên trình duyệt không mở thẳng AudioUrl; backend đọc và trả tệp.
/// </summary>
public record GetAudioAssetQuery(string AssetKey) : IRequest<StoredAudio>;

public class GetAudioAssetQueryValidator : AbstractValidator<GetAudioAssetQuery>
{
    public const string AllowedPrefix = "audio-assets/";

    public GetAudioAssetQueryValidator()
    {
        RuleFor(x => x.AssetKey)
            .NotEmpty().WithMessage("Thiếu AssetKey của tệp âm thanh.")
            .MaximumLength(400).WithMessage("AssetKey không được vượt quá 400 ký tự.")
            .Must(key => key.StartsWith(AllowedPrefix, StringComparison.Ordinal) &&
                         key.EndsWith(".mp3", StringComparison.Ordinal) &&
                         !key.Contains("..", StringComparison.Ordinal))
            .WithMessage("AssetKey không hợp lệ.");
    }
}

public class GetAudioAssetQueryHandler : IRequestHandler<GetAudioAssetQuery, StoredAudio>
{
    private readonly IAudioStorageService _storage;

    public GetAudioAssetQueryHandler(IAudioStorageService storage)
    {
        _storage = storage;
    }

    public async Task<StoredAudio> Handle(GetAudioAssetQuery request, CancellationToken cancellationToken)
    {
        return await _storage.ReadAudioAsync(request.AssetKey, cancellationToken)
            ?? throw new NotFoundException("AudioAsset", request.AssetKey);
    }
}
