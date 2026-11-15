using WaterbusSystem.Application.Features.SightseeingAudio.Models;

namespace WaterbusSystem.Application.Interfaces.Services;

/// <summary>Quyết định thông báo "sắp tới / đã tới / rời bến" từ vị trí tàu (tàu thường và tàu tham quan).</summary>
public interface IStationAnnouncementDecisionService
{
    Task<StationAnnouncementResult> EvaluateAndTriggerAsync(
        BoatPositionSnapshot position, TripStationContext context, CancellationToken cancellationToken = default);
}
