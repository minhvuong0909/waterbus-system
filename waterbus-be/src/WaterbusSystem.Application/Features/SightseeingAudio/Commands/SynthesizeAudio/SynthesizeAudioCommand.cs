using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Common.Models;

namespace WaterbusSystem.Application.Features.SightseeingAudio.Commands.SynthesizeAudio;

/// <summary>
/// DTO trả về sau khi tổng hợp file âm thanh MP3 từ văn bản qua Azure AI TTS.
/// </summary>
public record SynthesizeAudioDto(
    string AudioUrl,
    string AssetKey,
    string ContentType,
    long ByteLength,
    int DurationSeconds,
    string VoiceName);

/// <summary>
/// Lệnh sinh tệp âm thanh MP3 từ kịch bản văn bản tiếng Việt bằng Azure AI TTS (Neural Voice) và lưu trữ Blob.
/// Hoàn toàn độc lập, không truy cập CSDL và không phụ thuộc POI.
/// </summary>
public record SynthesizeAudioCommand : IRequest<SynthesizeAudioDto>
{
    public string Text { get; init; } = string.Empty;
    public string? VoiceName { get; init; }

    public SynthesizeAudioCommand() { }

    public SynthesizeAudioCommand(string text, string? voiceName = null)
    {
        Text = text;
        VoiceName = voiceName;
    }
}

public class SynthesizeAudioCommandValidator : AbstractValidator<SynthesizeAudioCommand>
{
    public SynthesizeAudioCommandValidator()
    {
        RuleFor(x => x.Text)
            .NotEmpty().WithMessage("Văn bản kịch bản không được để trống.")
            .MaximumLength(10_000).WithMessage("Kịch bản không được vượt quá 10.000 ký tự.");
    }
}

public class SynthesizeAudioCommandHandler : IRequestHandler<SynthesizeAudioCommand, SynthesizeAudioDto>
{
    private readonly ITextToSpeechService _tts;
    private readonly IAudioStorageService _storage;
    private readonly IDateTimeService _dateTime;

    public SynthesizeAudioCommandHandler(
        ITextToSpeechService tts,
        IAudioStorageService storage,
        IDateTimeService dateTime)
    {
        _tts = tts;
        _storage = storage;
        _dateTime = dateTime;
    }

    public async Task<SynthesizeAudioDto> Handle(SynthesizeAudioCommand request, CancellationToken cancellationToken)
    {
        var options = string.IsNullOrWhiteSpace(request.VoiceName) || request.VoiceName.Equals("string", StringComparison.OrdinalIgnoreCase)
            ? null
            : new TextToSpeechOptions { VoiceName = request.VoiceName.Trim() };

        // 1. Chuyển đổi kịch bản văn bản tiếng Việt sang dữ liệu nhị phân MP3 qua Azure TTS
        var audio = await _tts.SynthesizeSpeechAsync(request.Text, options, cancellationToken);

        // 2. Tạo AssetKey định danh lưu trữ duy nhất theo ngày
        var assetKey = $"audio-assets/tts/{_dateTime.Now:yyyyMMdd}/{Guid.NewGuid():N}.mp3";

        // 3. Tải tệp MP3 lên kho lưu trữ và lấy đường dẫn URL bền vững
        var stored = await _storage.StoreAudioAsync(new StoreAudioCommand
        {
            AudioData = audio.AudioData,
            AssetKey = assetKey,
            ContentType = audio.ContentType,
            LanguageCode = "vi",
            Duration = audio.Duration
        }, cancellationToken);

        return new SynthesizeAudioDto(
            stored.DurableAssetUri,
            assetKey,
            stored.ContentType,
            stored.ByteLength,
            (int)Math.Ceiling(audio.Duration.TotalSeconds),
            audio.VoiceName);
    }
}
