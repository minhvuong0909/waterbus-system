using WaterbusSystem.Application.DTOs.Requests.SightseeingAudio;
using WaterbusSystem.Application.DTOs.Responses.SightseeingAudio;
using WaterbusSystem.Application.Common.Models;

namespace WaterbusSystem.Application.Interfaces.Services;

public interface IAudioSynthesisService
{
    Task<SynthesizeAudioResponse> SynthesizeAsync(SynthesizeAudioRequest request, CancellationToken cancellationToken = default);
}
