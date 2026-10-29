using WaterbusSystem.Application.Features.SightseeingAudio.Models;

namespace WaterbusSystem.Application.Interfaces.Services;

/// <summary>Sinh MP3 từ kịch bản tiếng Việt rồi lưu asset private; không ghi AudioGuide hay publish.</summary>
public interface IAudioAssetGenerationService
{
    Task<AudioAssetGenerationResult> GenerateAsync(
        GenerateAudioAssetRequest request,
        CancellationToken cancellationToken = default);
}
