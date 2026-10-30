using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Application.Features.SightseeingAudio.Models;
using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Application.Services.SightseeingAudio;

/// <summary>Áp dụng điều kiện hành trình rồi gọi core geofence bằng snapshot từ repository.</summary>
public sealed class TrackingAudioTriggerEvaluator(ISightseeingAudioRepository repository, IPoiAudioTriggerDecisionService decisionService) : ITrackingAudioTriggerEvaluator
{
    public async Task<PoiAudioTriggerEvaluationResult?> EvaluateAsync(BoatPositionSnapshot position, CancellationToken cancellationToken = default)
    {
        if (position.TripId is not Guid tripId) return null;
        var snapshot = await repository.GetTrackingSnapshotAsync(tripId, position.BoatId, cancellationToken);
        if (snapshot is null || snapshot.Trip.TripType != TripType.Sightseeing || snapshot.Trip.Status != TripStatus.EnRoute) return null;
        return await decisionService.EvaluateAndTriggerAsync(position, snapshot.Trip, snapshot.Candidates, cancellationToken);
    }
}
