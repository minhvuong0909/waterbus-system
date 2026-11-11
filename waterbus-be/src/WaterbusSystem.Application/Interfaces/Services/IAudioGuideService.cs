using WaterbusSystem.Application.DTOs.Requests.SightseeingAudio;
using WaterbusSystem.Application.DTOs.Responses.SightseeingAudio;
using WaterbusSystem.Application.Common.Models;

namespace WaterbusSystem.Application.Interfaces.Services;

public interface IAudioGuideService
{
    Task<PublishedAudioGuideResponse> PublishAsync(PublishAudioGuideRequest request, CancellationToken cancellationToken = default);
    Task<AudioGuidePoisPageResponse> GetPoisAsync(GetAudioGuidePoisRequest request, CancellationToken cancellationToken = default);
}
