using WaterbusSystem.Application.Interfaces.Persistence;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Exceptions;
using FluentValidation;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.SightseeingAudio;
using WaterbusSystem.Application.DTOs.Responses.SightseeingAudio;
using WaterbusSystem.Application.Features.SightseeingAudio;
using WaterbusSystem.Application.Interfaces.Services;
using ValidationException = WaterbusSystem.Application.Common.Exceptions.ValidationException;
using ValidationFailure = FluentValidation.Results.ValidationFailure;

namespace WaterbusSystem.Application.Services.SightseeingAudio;

/// <summary>
/// Đọc danh mục audio và publish MP3 đã duyệt cho POI.
/// Bài thuyết minh chỉ được publish khi thời lượng nằm trong ngân sách thời gian của chặng,
/// nếu không trên tàu sẽ bị câu thông báo "sắp đến bến" đè lên.
/// </summary>
public sealed class AudioGuideService(
    ISightseeingAudioRepository repository, IUnitOfWork unitOfWork, IAudioStorageService storage,
    IValidator<PublishAudioGuideRequest> publishValidator, IValidator<GetAudioGuidePoisRequest> listValidator,
    IStopAnnouncementConfigStore configStore) : IAudioGuideService
{
    private readonly ISightseeingAudioRepository _repository = repository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IAudioStorageService _storage = storage;
    private readonly IValidator<PublishAudioGuideRequest> _publishValidator = publishValidator;

    public async Task<AudioGuidePoisPageResponse> GetPoisAsync(GetAudioGuidePoisRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await listValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) throw new ValidationException(validation.Errors);
        return await _repository.GetPoisAsync(request.Page, request.PageSize, cancellationToken);
    }

    public async Task<PublishedAudioGuideResponse> PublishAsync(PublishAudioGuideRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await _publishValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) throw new ValidationException(validation.Errors);
        var poiExists = await _repository.ActivePoiExistsAsync(request.PoiId, cancellationToken);
        if (!poiExists)
            throw new NotFoundException(nameof(PointOfInterest), request.PoiId);

        // ExistsAsync chỉ chấp nhận URI của đúng container Blob được cấu hình và MP3 có thật.
        if (!await _storage.ExistsAsync(request.AudioAssetUri, cancellationToken))
            throw new NotFoundException("AudioAsset", request.AudioAssetUri);

        await EnsureNarrationFitsTimeBudgetAsync(request, cancellationToken);

        var guide = await _repository.FindGuideForUpdateAsync(request.PoiId, "vi", cancellationToken);

        if (guide is null)
        {
            guide = new AudioGuide { PoiId = request.PoiId, LanguageCode = "vi" };
            _repository.AddGuide(guide);
        }

        // Gọi lại cùng URI là idempotent; URI mới thay thế bản demo/phiên bản cũ.
        if (guide.IsDeleted || !guide.IsPublished || guide.AudioAssetUri != request.AudioAssetUri
            || guide.DurationSeconds != request.DurationSeconds)
        {
            guide.AudioAssetUri = request.AudioAssetUri;
            guide.DurationSeconds = request.DurationSeconds;
            guide.IsPublished = true;
            guide.IsDeleted = false;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new PublishedAudioGuideResponse(guide.Id, guide.PoiId, guide.LanguageCode,
            guide.AudioAssetUri, guide.DurationSeconds, guide.IsPublished);
    }

    private async Task EnsureNarrationFitsTimeBudgetAsync(
        PublishAudioGuideRequest request, CancellationToken cancellationToken)
    {
        if (!request.DurationSeconds.HasValue) return;

        var options = configStore.Current;
        var maxSegments = await _repository.GetMaxRouteSegmentCountAsync(cancellationToken);
        var segmentCount = maxSegments > 0 ? maxSegments : Math.Max(1, options.EstimatedSegmentCount);
        var budget = StopAnnouncementTimeBudgetCalculator.Calculate(options, segmentCount);

        if (request.DurationSeconds.Value > budget.MaxNarrationSeconds)
        {
            throw new ValidationException(new[]
            {
                new ValidationFailure("DurationSeconds",
                    $"Thuyết minh dài {request.DurationSeconds.Value} giây vượt quá ngân sách "
                    + $"{budget.MaxNarrationSeconds:F0} giây của một chặng. "
                    + "Rút ngắn kịch bản hoặc tăng thời lượng toàn chuyến ở tab Cấu hình mô phỏng.")
            });
        }
    }
}
