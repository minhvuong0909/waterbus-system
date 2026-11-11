using Microsoft.Extensions.Options;
using WaterbusSystem.Application.Features.SightseeingAudio;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Application.Features.SightseeingAudio.Models;
using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Application.Services.SightseeingAudio;

/// <summary>Áp dụng điều kiện hành trình rồi gọi core geofence bằng snapshot từ repository.</summary>
public sealed class TrackingAudioTriggerService(ISightseeingAudioRepository repository, IPoiAudioTriggerDecisionService decisionService,
    IOptions<SightseeingAudioOptions>? options = null) : ITrackingAudioTriggerService
{
    public async Task<PoiAudioTriggerEvaluationResult?> EvaluateAsync(BoatPositionSnapshot position, CancellationToken cancellationToken = default)
    {
        if (position.TripId is not Guid tripId) return null;
        // Vị trí lỗi thời (GPS mất/đến trễ) không được kích hoạt audio: tàu có thể đã đi qua POI.
        var maxAge = (options?.Value ?? new SightseeingAudioOptions()).MaxPositionAgeSeconds;
        if (maxAge > 0 && DateTimeOffset.UtcNow - position.ObservedAt > TimeSpan.FromSeconds(maxAge))
            return PoiAudioTriggerEvaluationResult.Skipped(TriggerDecisionReason.StalePosition, "Mẫu GPS quá cũ, bỏ qua kích hoạt audio.");
        var snapshot = await repository.GetTrackingSnapshotAsync(tripId, position.BoatId, cancellationToken);
        if (snapshot is null || snapshot.Trip.TripType != TripType.Sightseeing || snapshot.Trip.Status != TripStatus.EnRoute) return null;
        return await decisionService.EvaluateAndTriggerAsync(position, snapshot.Trip, snapshot.Candidates, cancellationToken);
    }
}
