using FluentValidation;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.SightseeingAudio;
using WaterbusSystem.Application.DTOs.Responses.SightseeingAudio;
using WaterbusSystem.Application.Interfaces.Services;
using ValidationException = WaterbusSystem.Application.Common.Exceptions.ValidationException;

namespace WaterbusSystem.Application.Services.SightseeingAudio;

public class AudioSynthesisService : IAudioSynthesisService
{
    private readonly ITextToSpeechService _tts;
    private readonly IAudioStorageService _storage;
    private readonly IDateTimeService _dateTime;
    private readonly IValidator<SynthesizeAudioRequest> _validator;

    public AudioSynthesisService(
        ITextToSpeechService tts,
        IAudioStorageService storage,
        IDateTimeService dateTime, IValidator<SynthesizeAudioRequest> validator)
    {
        _tts = tts;
        _storage = storage;
        _dateTime = dateTime;
        _validator = validator;
    }

    public async Task<SynthesizeAudioResponse> SynthesizeAsync(SynthesizeAudioRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) throw new ValidationException(validation.Errors);

        var options = string.IsNullOrWhiteSpace(request.VoiceName) || request.VoiceName.Equals("string", StringComparison.OrdinalIgnoreCase)
            ? null
            : new TextToSpeechOptions { VoiceName = request.VoiceName.Trim() };

        // 1. Chuyển đổi kịch bản văn bản tiếng Việt sang dữ liệu nhị phân MP3 qua Azure TTS
        var audio = await _tts.SynthesizeSpeechAsync(request.Text, options, cancellationToken);

        // 2. Tạo AssetKey định danh lưu trữ duy nhất theo ngày
        var assetKey = $"audio-assets/tts/{_dateTime.Now:yyyyMMdd}/{Guid.NewGuid():N}.mp3";

        // 3. Tải tệp MP3 lên kho lưu trữ và lấy đường dẫn URL bền vững
        var stored = await _storage.StoreAudioAsync(new StoreAudioRequest
        {
            AudioData = audio.AudioData,
            AssetKey = assetKey,
            ContentType = audio.ContentType,
            LanguageCode = "vi",
            Duration = audio.Duration
        }, cancellationToken);

        return new SynthesizeAudioResponse(
            stored.DurableAssetUri,
            assetKey,
            stored.ContentType,
            stored.ByteLength,
            (int)Math.Ceiling(audio.Duration.TotalSeconds),
            audio.VoiceName);
    }
}
