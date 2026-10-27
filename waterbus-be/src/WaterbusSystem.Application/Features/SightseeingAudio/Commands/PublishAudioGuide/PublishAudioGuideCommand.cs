using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Exceptions;

namespace WaterbusSystem.Application.Features.SightseeingAudio.Commands.PublishAudioGuide;

/// <summary>Admin duyệt MP3 đã sinh trên Azure Blob cho một POI tiếng Việt.</summary>
public record PublishAudioGuideCommand(Guid PoiId, string AudioAssetUri, int? DurationSeconds)
    : IRequest<PublishedAudioGuideDto>;

public record PublishedAudioGuideDto(Guid AudioGuideId, Guid PoiId, string LanguageCode,
    string AudioAssetUri, int? DurationSeconds, bool IsPublished);

public sealed class PublishAudioGuideCommandValidator : AbstractValidator<PublishAudioGuideCommand>
{
    public PublishAudioGuideCommandValidator()
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

public sealed class PublishAudioGuideCommandHandler
    : IRequestHandler<PublishAudioGuideCommand, PublishedAudioGuideDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IAudioStorageService _storage;

    public PublishAudioGuideCommandHandler(IApplicationDbContext context, IAudioStorageService storage)
    {
        _context = context;
        _storage = storage;
    }

    public async Task<PublishedAudioGuideDto> Handle(PublishAudioGuideCommand request,
        CancellationToken cancellationToken)
    {
        var poiExists = await _context.PointsOfInterest.AsNoTracking()
            .AnyAsync(p => p.Id == request.PoiId && !p.IsDeleted && p.IsActive, cancellationToken);
        if (!poiExists)
            throw new NotFoundException(nameof(PointOfInterest), request.PoiId);

        // ExistsAsync chỉ chấp nhận URI của đúng container Blob được cấu hình và MP3 có thật.
        if (!await _storage.ExistsAsync(request.AudioAssetUri, cancellationToken))
            throw new NotFoundException("AudioAsset", request.AudioAssetUri);

        var guide = await _context.AudioGuides
            .FirstOrDefaultAsync(a => a.PoiId == request.PoiId && a.LanguageCode == "vi", cancellationToken);

        if (guide is null)
        {
            guide = new AudioGuide { PoiId = request.PoiId, LanguageCode = "vi" };
            _context.AudioGuides.Add(guide);
        }

        // Gọi lại cùng URI là idempotent; URI mới thay thế bản demo/phiên bản cũ.
        if (guide.IsDeleted || !guide.IsPublished || guide.AudioAssetUri != request.AudioAssetUri
            || guide.DurationSeconds != request.DurationSeconds)
        {
            guide.AudioAssetUri = request.AudioAssetUri;
            guide.DurationSeconds = request.DurationSeconds;
            guide.IsPublished = true;
            guide.IsDeleted = false;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return new PublishedAudioGuideDto(guide.Id, guide.PoiId, guide.LanguageCode,
            guide.AudioAssetUri, guide.DurationSeconds, guide.IsPublished);
    }
}
