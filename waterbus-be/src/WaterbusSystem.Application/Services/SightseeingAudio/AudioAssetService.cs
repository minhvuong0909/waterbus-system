using WaterbusSystem.Domain.Exceptions;
using FluentValidation;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.SightseeingAudio;
using WaterbusSystem.Application.DTOs.Responses.SightseeingAudio;
using WaterbusSystem.Application.Interfaces.Services;
using ValidationException = WaterbusSystem.Application.Common.Exceptions.ValidationException;

namespace WaterbusSystem.Application.Services.SightseeingAudio;

public class AudioAssetService : IAudioAssetService
{
    private readonly IAudioStorageService _storage;
    private readonly IValidator<GetAudioAssetRequest> _validator;

    public AudioAssetService(IAudioStorageService storage, IValidator<GetAudioAssetRequest> validator)
    {
        _storage = storage;
        _validator = validator;
    }

    public async Task<StoredAudio> GetAsync(GetAudioAssetRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) throw new ValidationException(validation.Errors);

        return await _storage.ReadAudioAsync(request.AssetKey, cancellationToken)
            ?? throw new NotFoundException("AudioAsset", request.AssetKey);
    }
}
