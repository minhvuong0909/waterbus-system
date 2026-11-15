using WaterbusSystem.Application.DTOs.Responses.SightseeingAudio;
using WaterbusSystem.Application.Features.SightseeingAudio.Models;
using WaterbusSystem.Domain.Entities;

namespace WaterbusSystem.Application.Interfaces.Repositories;

/// <summary>Truy cập dữ liệu POI/audio và snapshot hành trình cho geofence.</summary>
public interface ISightseeingAudioRepository
{
    Task<bool> ActivePoiExistsAsync(Guid poiId, CancellationToken cancellationToken);
    Task<AudioGuide?> FindGuideForUpdateAsync(Guid poiId, string languageCode, CancellationToken cancellationToken);
    void AddGuide(AudioGuide guide);
    Task<AudioGuidePoisPageResponse> GetPoisAsync(int pageNumber, int pageSize, CancellationToken cancellationToken);
    Task<SightseeingAudioSnapshot?> GetTrackingSnapshotAsync(Guid tripId, Guid boatId, CancellationToken cancellationToken);
    /// <summary>Các điểm dừng của chuyến theo VisitOrder kèm clip thông báo bến (cho tàu thường và tàu tham quan).</summary>
    Task<TripStationContext> GetStationSnapshotAsync(TripSightseeingContext trip, CancellationToken cancellationToken);
}
