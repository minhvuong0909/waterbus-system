using Microsoft.Extensions.Logging;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.Features.SightseeingAudio;
using WaterbusSystem.Application.Features.SightseeingAudio.Models;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Application.Features.SightseeingAudio.Services;

/// <summary>
/// Điều phối phát thông báo bến cho mọi loại chuyến và thuyết minh địa danh cho chuyến Sightseeing.
/// - Chuyến Commuter: chỉ thông báo rời bến / gần tới bến / đã tới bến (BR-EXP-01).
/// - Chuyến Sightseeing: khi rời bến kèm bài thuyết minh địa danh của chặng đang đi.
/// Service không gọi SignalR trực tiếp, chỉ phụ thuộc interface publisher.
/// </summary>
public sealed class StopAnnouncementService(
    ISightseeingAudioRepository repository,
    IStopAnnouncementPublisher publisher,
    IStopAnnouncementEmissionGuard guard,
    IGeoDistanceCalculator distanceCalculator,
    IDateTimeService dateTime,
    IStopAnnouncementConfigStore configStore,
    ILogger<StopAnnouncementService> logger) : IStopAnnouncementService
{
    public async Task<bool> AnnounceDepartingAsync(
        Guid tripId, int fromVisitOrder, CancellationToken cancellationToken = default)
    {
        var snapshot = await repository.GetStopAnnouncementSnapshotAsync(tripId, cancellationToken);
        if (snapshot is null || snapshot.BoatId == Guid.Empty) return false;

        var fromIndex = IndexOfVisitOrder(snapshot, fromVisitOrder);
        if (fromIndex < 0 || fromIndex >= snapshot.Stations.Count - 1) return false;

        var from = snapshot.Stations[fromIndex];
        var to = snapshot.Stations[fromIndex + 1];

        if (!guard.TryClaim(tripId, fromIndex, "departing")) return false;

        var narration = await BuildNarrationAsync(snapshot, from, to, cancellationToken);

        await publisher.PublishDepartingAsync(
            snapshot.TripId, snapshot.BoatId, from.StationId, from.Name, dateTime.Now,
            snapshot.TripType, to.Name, narration, cancellationToken);

        logger.LogInformation(
            "[STOP-ANNOUNCE] Chuyến {TripId} ({TripType}) rời bến {Station} đi {NextStation}; thuyết minh: {Narration}",
            snapshot.TripId, snapshot.TripType, from.Name, to.Name,
            narration is null ? "không có" : narration.PoiName);

        return true;
    }

    public async Task<bool> AnnounceNearStopAsync(
        Guid tripId, int toVisitOrder, double segmentProgress, CancellationToken cancellationToken = default)
    {
        var snapshot = await repository.GetStopAnnouncementSnapshotAsync(tripId, cancellationToken);
        if (snapshot is null || snapshot.BoatId == Guid.Empty) return false;

        var toIndex = IndexOfVisitOrder(snapshot, toVisitOrder);
        if (toIndex <= 0) return false;

        var to = snapshot.Stations[toIndex];
        if (!guard.TryClaim(tripId, toIndex - 1, "near")) return false;

        await publisher.PublishNearStopAsync(
            snapshot.TripId, snapshot.BoatId, to.StationId, to.Name, segmentProgress,
            dateTime.Now, snapshot.TripType, cancellationToken);

        return true;
    }

    public async Task<bool> AnnounceArrivedAsync(
        Guid tripId, int toVisitOrder, double segmentProgress, CancellationToken cancellationToken = default)
    {
        var snapshot = await repository.GetStopAnnouncementSnapshotAsync(tripId, cancellationToken);
        if (snapshot is null || snapshot.BoatId == Guid.Empty) return false;

        var toIndex = IndexOfVisitOrder(snapshot, toVisitOrder);
        if (toIndex <= 0) return false;

        var to = snapshot.Stations[toIndex];
        if (!guard.TryClaim(tripId, toIndex - 1, "arrived")) return false;

        await publisher.PublishArrivedAsync(
            snapshot.TripId, snapshot.BoatId, to.StationId, to.Name, segmentProgress,
            dateTime.Now, snapshot.TripType, cancellationToken);

        return true;
    }

    /// <summary>
    /// Chọn địa danh để thuyết minh cho chặng đang đi: địa danh gần giữa chặng nhất,
    /// mỗi địa danh chỉ thuyết minh một lần trên mỗi chuyến.
    /// Bài thuyết minh dài hơn ngân sách thời gian của chặng sẽ không được phát,
    /// để không bị câu thông báo "sắp đến bến" đè lên giữa chừng.
    /// </summary>
    private async Task<StopNarrationPayload?> BuildNarrationAsync(
        StopAnnouncementSnapshot snapshot,
        StopAnnouncementStation from,
        StopAnnouncementStation to,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(snapshot.TripType, nameof(TripType.Sightseeing), StringComparison.Ordinal)) return null;

        var candidates = await repository.ListRouteNarrationCandidatesAsync(snapshot.RouteId, cancellationToken);
        var located = candidates.Where(c => c.Latitude.HasValue && c.Longitude.HasValue).ToList();
        if (located.Count == 0) return null;

        var midLat = (from.Latitude + to.Latitude) / 2m;
        var midLng = (from.Longitude + to.Longitude) / 2m;

        var nearest = located
            .Select(c => new
            {
                Candidate = c,
                Distance = distanceCalculator.CalculateDistanceMeters(
                    midLat, midLng, c.Latitude!.Value, c.Longitude!.Value)
            })
            .OrderBy(x => x.Distance)
            .First();

        if (!guard.TryClaimNarration(snapshot.TripId, nearest.Candidate.PoiId)) return null;

        var assetKey = AudioAssetIdentifiers.ToAssetKey(nearest.Candidate.AudioAssetUri);
        var hasStableAsset = AudioAssetIdentifiers.IsStable(nearest.Candidate.AudioAssetUri)
            && !string.IsNullOrWhiteSpace(assetKey);

        var durationSeconds = nearest.Candidate.DurationSeconds;
        var maxNarrationSeconds = await MaxNarrationSecondsAsync(snapshot.RouteId, cancellationToken);
        if (durationSeconds.HasValue && durationSeconds.Value > maxNarrationSeconds)
        {
            logger.LogWarning(
                "[STOP-ANNOUNCE] Bỏ qua thuyết minh {PoiName} ({Duration}s) vượt quá ngân sách thời gian của chặng.",
                nearest.Candidate.PoiName, durationSeconds.Value);
            return null;
        }

        var maxCharacters = await MaxNarrationCharactersAsync(snapshot.RouteId, cancellationToken);
        var text = TruncateToCharacters(
            BuildNarrationText(nearest.Candidate.PoiName, nearest.Candidate.Description),
            maxCharacters);

        return new StopNarrationPayload(
            nearest.Candidate.PoiId,
            nearest.Candidate.PoiName,
            text,
            hasStableAsset ? assetKey : null,
            durationSeconds);
    }

    private async Task<(double Seconds, int Characters)> MaxNarrationLimitsAsync(
        Guid routeId, CancellationToken cancellationToken)
    {
        var options = configStore.Current;
        var maxSegments = await repository.GetMaxRouteSegmentCountAsync(cancellationToken);
        var segmentCount = maxSegments > 0 ? maxSegments : Math.Max(1, options.EstimatedSegmentCount);
        var budget = StopAnnouncementTimeBudgetCalculator.Calculate(options, segmentCount);
        return (budget.MaxNarrationSeconds, budget.MaxNarrationCharacters);
    }

    private async Task<double> MaxNarrationSecondsAsync(Guid routeId, CancellationToken cancellationToken) =>
        (await MaxNarrationLimitsAsync(routeId, cancellationToken)).Seconds;

    private async Task<int> MaxNarrationCharactersAsync(Guid routeId, CancellationToken cancellationToken) =>
        (await MaxNarrationLimitsAsync(routeId, cancellationToken)).Characters;

    /// <summary>
    /// Cắt kịch bản thuyết minh theo giới hạn ký tự của ngân sách thời gian.
    /// Captain đọc trực tiếp bằng TTS nên thời lượng thực tế phụ thuộc độ dài kịch bản,
    /// không chỉ phụ thuộc file MP3 đã publish.
    /// </summary>
    private static string TruncateToCharacters(string text, int maxCharacters)
    {
        if (maxCharacters <= 0 || text.Length <= maxCharacters) return text;

        var truncated = text[..maxCharacters];
        var lastStop = truncated.LastIndexOfAny(['.', '!', '?']);
        if (lastStop > maxCharacters / 2) return truncated[..(lastStop + 1)];
        return truncated.TrimEnd() + ".";
    }

    private static string BuildNarrationText(string poiName, string? description)
    {
        var body = string.IsNullOrWhiteSpace(description)
            ? $"Đây là {poiName}, một điểm tham quan nổi tiếng dọc sông Sài Gòn."
            : description.Trim();
        return $"Quý khách tham quan {poiName}. {body}";
    }

    private static int IndexOfVisitOrder(StopAnnouncementSnapshot snapshot, int visitOrder)
    {
        for (var i = 0; i < snapshot.Stations.Count; i++)
        {
            if (snapshot.Stations[i].VisitOrder == visitOrder) return i;
        }
        return -1;
    }
}
