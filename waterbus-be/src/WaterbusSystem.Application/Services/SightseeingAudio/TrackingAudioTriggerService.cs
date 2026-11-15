using Microsoft.Extensions.Options;
using WaterbusSystem.Application.Features.SightseeingAudio;
using WaterbusSystem.Application.Features.SightseeingAudio.Interfaces;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Application.Features.SightseeingAudio.Models;
using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Application.Services.SightseeingAudio;

/// <summary>
/// Áp dụng điều kiện hành trình rồi gọi core geofence bằng snapshot từ repository: thông báo bến cho mọi chuyến EnRoute
/// (tàu thường và tàu tham quan), thuyết minh địa danh thêm cho tàu tham quan. Mỗi lần GPS tối đa một lệnh: bến được ưu tiên,
/// địa danh xét ở lần GPS kế tiếp.
/// </summary>
public sealed class TrackingAudioTriggerService(ISightseeingAudioRepository repository, IPoiAudioTriggerDecisionService decisionService,
    IStationAnnouncementDecisionService stationService, IPoiTriggerDeduplicationService deduplication,
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
        if (snapshot is null || snapshot.Trip.Status != TripStatus.EnRoute) return null;

        var station = await stationService.EvaluateAndTriggerAsync(
            position, await repository.GetStationSnapshotAsync(snapshot.Trip, cancellationToken), cancellationToken);
        if (station.Triggered)
            return new PoiAudioTriggerEvaluationResult { ShouldTrigger = true, Reason = TriggerDecisionReason.Triggered, Message = station.Message };

        if (snapshot.Trip.TripType != TripType.Sightseeing) return null;

        // Tiến độ hành trình: chỉ xét địa danh kế tiếp chưa phát theo DisplayOrder (vùng của nhiều địa danh có thể chồng nhau,
        // và có địa danh nằm trong đất liền nên bán kính lớn). Địa danh đã Accepted/hết lượt thử được coi là đã qua.
        var next = (int?)null;
        foreach (var candidate in snapshot.Candidates.Where(c => c.IsPoiActive && c.IsRoutePoiActive).OrderBy(c => c.DisplayOrder))
        {
            var key = candidate.RoutePoiId == Guid.Empty ? candidate.PoiId : candidate.RoutePoiId;
            if (await deduplication.HasBeenTriggeredAsync(snapshot.Trip.TripId, key, null, cancellationToken)) continue;
            next = candidate.DisplayOrder;
            break;
        }
        if (next is null) return null;
        return await decisionService.EvaluateAndTriggerAsync(position, snapshot.Trip with { NextRoutePoiDisplayOrder = next }, snapshot.Candidates, cancellationToken);
    }
}
