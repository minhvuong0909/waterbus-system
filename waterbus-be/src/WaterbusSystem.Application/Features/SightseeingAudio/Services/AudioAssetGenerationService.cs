using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.Features.SightseeingAudio.Interfaces;
using WaterbusSystem.Application.Features.SightseeingAudio.Models;

namespace WaterbusSystem.Application.Features.SightseeingAudio.Services;

/// <summary>
/// Ghép hai ranh giới thuộc Thịnh: Azure Speech và Azure Blob.
/// Không truy cập DB, không tự suy ra version kịch bản hoặc quyền publish.
/// </summary>
public sealed class AudioAssetGenerationService : IAudioAssetGenerationService
{
    private readonly ITextToSpeechService _textToSpeech;
    private readonly IAudioStorageService _storage;

    public AudioAssetGenerationService(ITextToSpeechService textToSpeech, IAudioStorageService storage)
    {
        _textToSpeech = textToSpeech ?? throw new ArgumentNullException(nameof(textToSpeech));
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
    }

    public async Task<AudioAssetGenerationResult> GenerateAsync(
        GenerateAudioAssetRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.PoiId == Guid.Empty)
            throw new ArgumentException("PoiId không được rỗng.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.ApprovedScriptText))
            throw new ArgumentException("Kịch bản đã duyệt không được rỗng.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.AssetKey))
            throw new ArgumentException("AssetKey có version không được rỗng.", nameof(request));

        var audio = await _textToSpeech.SynthesizeSpeechAsync(
            request.ApprovedScriptText,
            cancellationToken: cancellationToken);

        var stored = await _storage.StoreAudioAsync(new StoreAudioCommand
        {
            AudioData = audio.AudioData,
            AssetKey = request.AssetKey,
            ContentType = audio.ContentType,
            PoiId = request.PoiId,
            LanguageCode = "vi",
            Duration = audio.Duration
        }, cancellationToken);

        return new AudioAssetGenerationResult(
            request.PoiId,
            request.AssetKey,
            stored.DurableAssetUri,
            stored.ContentType,
            stored.ByteLength,
            (int)Math.Ceiling(audio.Duration.TotalSeconds),
            audio.VoiceName);
    }
}
