using WaterbusSystem.Application.DTOs.Requests.SightseeingAudio;
using WaterbusSystem.Application.DTOs.Responses.SightseeingAudio;
using WaterbusSystem.Application.Common.Models;

namespace WaterbusSystem.Application.Interfaces.Services;

public interface IAudioAssetService
{
    Task<StoredAudio> GetAsync(GetAudioAssetRequest request, CancellationToken cancellationToken = default);
}
