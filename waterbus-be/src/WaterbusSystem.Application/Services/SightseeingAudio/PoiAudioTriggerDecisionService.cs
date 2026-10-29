using WaterbusSystem.Application.Interfaces.Services;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.SightseeingAudio.Interfaces;
using WaterbusSystem.Application.Features.SightseeingAudio.Models;
using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Application.Services.SightseeingAudio;

/// <summary>
/// Triển khai dịch vụ đánh giá và ra quyết định kích hoạt audio thuyết minh Sightseeing
/// khi tàu đi vào vùng bán kính địa danh (POI geofence).
/// Tuân thủ BR-EXP-01..07, BR-INC-05:
/// - Chỉ kích hoạt cho tuyến/chuyến Sightseeing đang EnRoute trên đúng tàu
/// - Bán kính TriggerRadiusM null không tự đặt mặc định (giữ nguyên không trigger)
/// - Yêu cầu POI & RoutePoi active, tọa độ hợp lệ, AudioGuide tiếng Việt đã publish
/// - Chặn khi Suspended, Terminated, trạng thái dừng hoặc đang có thông báo an toàn / Captain override
/// - Ngăn trigger lặp trong cùng lượt ghé qua IPoiTriggerDeduplicationService
/// - Phân tách gửi yêu cầu phát qua IOnboardAudioTriggerDispatcher; chưa có ACK từ loa tàu thì không báo đã phát.
/// </summary>
public sealed class PoiAudioTriggerDecisionService : IPoiAudioTriggerDecisionService
{
    private readonly IGeoDistanceCalculator _distanceCalculator;
    private readonly IPoiTriggerDeduplicationService _deduplicationService;
    private readonly IOnboardAudioTriggerDispatcher _dispatcher;
    private readonly ILogger<PoiAudioTriggerDecisionService>? _logger;

    public PoiAudioTriggerDecisionService(
        IGeoDistanceCalculator distanceCalculator,
        IPoiTriggerDeduplicationService deduplicationService,
        IOnboardAudioTriggerDispatcher dispatcher,
        ILogger<PoiAudioTriggerDecisionService>? logger = null)
    {
        _distanceCalculator = distanceCalculator ?? throw new ArgumentNullException(nameof(distanceCalculator));
        _deduplicationService = deduplicationService ?? throw new ArgumentNullException(nameof(deduplicationService));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _logger = logger;
    }

    public async Task<PoiAudioTriggerEvaluationResult> EvaluateAndTriggerAsync(
        BoatPositionSnapshot position,
        TripSightseeingContext tripContext,
        IReadOnlyList<PoiGeofenceCandidate> poiCandidates,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(position);
        ArgumentNullException.ThrowIfNull(tripContext);
        cancellationToken.ThrowIfCancellationRequested();

        if (tripContext.TripId == Guid.Empty || tripContext.BoatId == Guid.Empty || tripContext.RouteId == Guid.Empty)
            return PoiAudioTriggerEvaluationResult.Skipped(TriggerDecisionReason.InvalidTripContext,
                "Ngữ cảnh Trip/Boat/Route không hợp lệ.");

        if (tripContext.NextRoutePoiDisplayOrder is <= 0)
            return PoiAudioTriggerEvaluationResult.Skipped(TriggerDecisionReason.InvalidTripContext,
                "Thứ tự RoutePoi tiếp theo phải là số dương.");

        if (position.BoatId == Guid.Empty || !position.TripId.HasValue || position.TripId == Guid.Empty
            || position.ObservedAt == default
            || position.Latitude is < -90m or > 90m || position.Longitude is < -180m or > 180m)
            return PoiAudioTriggerEvaluationResult.Skipped(TriggerDecisionReason.InvalidBoatPosition,
                "Mẫu vị trí Boat thiếu TripId/timestamp hoặc tọa độ hợp lệ.");

        if (poiCandidates == null || poiCandidates.Count == 0)
        {
            return PoiAudioTriggerEvaluationResult.Skipped(
                TriggerDecisionReason.NoCandidatePois,
                "Không có ứng viên POI nào được cung cấp trên tuyến.");
        }

        // 1. Kiểm tra khớp BoatId
        if (position.BoatId != tripContext.BoatId)
        {
            return PoiAudioTriggerEvaluationResult.Skipped(
                TriggerDecisionReason.BoatTripMismatch,
                $"BoatId trong dữ liệu vị trí ({position.BoatId}) không khớp với BoatId của chuyến tàu ({tripContext.BoatId}).");
        }

        // 2. Kiểm tra khớp TripId (mẫu GPS không có TripId đã bị loại ở trên)
        if (position.TripId.Value != tripContext.TripId)
        {
            return PoiAudioTriggerEvaluationResult.Skipped(
                TriggerDecisionReason.BoatTripMismatch,
                $"TripId trong dữ liệu vị trí ({position.TripId.Value}) không khớp với TripId của chuyến tàu ({tripContext.TripId}).");
        }

        // 3. Kiểm tra loại chuyến: Chỉ Sightseeing mới có thuyết minh (BR-EXP-01)
        if (tripContext.TripType != TripType.Sightseeing)
        {
            return PoiAudioTriggerEvaluationResult.Skipped(
                TriggerDecisionReason.TripNotSightseeing,
                $"Chuyến tàu thuộc loại '{tripContext.TripType}', không phải Sightseeing (BR-EXP-01: Chuyến Commuter không kích hoạt audio thuyết minh).");
        }

        // 4. Kiểm tra trạng thái chuyến: Chỉ EnRoute mới được phát audio (BR-EXP-05, BR-INC-05)
        if (tripContext.Status != TripStatus.EnRoute)
        {
            return PoiAudioTriggerEvaluationResult.Skipped(
                TriggerDecisionReason.TripNotEnRoute,
                $"Chuyến tàu đang ở trạng thái '{tripContext.Status}', không phải EnRoute (không phát khi Suspended, Terminated hoặc chưa/đã kết thúc hành trình).");
        }

        // 5. Kiểm tra thông báo an toàn / Captain override (BR-EXP-06)
        if (tripContext.IsSafetyAnnouncementActive)
        {
            return PoiAudioTriggerEvaluationResult.Skipped(
                TriggerDecisionReason.SafetyAnnouncementActive,
                "Đang có thông báo an toàn hoặc Captain override; audio tham quan phải nhường ưu tiên (BR-EXP-06).");
        }

        // 7. Lọc các POI thuộc đúng tuyến của chuyến
        var allRouteCandidates = poiCandidates
            .Where(c => c.RouteId == tripContext.RouteId)
            .ToList();

        if (allRouteCandidates.GroupBy(c => c.PoiId)
            .Any(group => group.Count() > 1 && group.Any(c => c.RoutePoiId == Guid.Empty)))
            return PoiAudioTriggerEvaluationResult.Skipped(
                TriggerDecisionReason.InvalidTripContext,
                "POI xuất hiện nhiều lần trên tuyến nhưng thiếu RoutePoiId để chống phát lặp đúng từng lần xuất hiện.");

        var routeCandidates = allRouteCandidates
            .Where(c => !tripContext.NextRoutePoiDisplayOrder.HasValue
                || c.DisplayOrder == tripContext.NextRoutePoiDisplayOrder.Value)
            .ToList();

        if (routeCandidates.Count == 0)
        {
            return PoiAudioTriggerEvaluationResult.Skipped(
                TriggerDecisionReason.NoCandidatePois,
                $"Không tìm thấy POI nào trên Route '{tripContext.RouteId}' của chuyến tàu.");
        }

        // 8. Đánh giá từng ứng viên POI
        var qualifiedMatches = new List<QualifyingPoiMatch>();
        bool encounteredAlreadyTriggered = false;
        bool encounteredMissingAudio = false;
        bool encounteredInvalidRadius = false;
        bool encounteredInactivePoi = false;
        bool encounteredInactiveRoutePoi = false;
        bool encounteredInvalidPoiCoordinates = false;
        bool encounteredMissingPoiCoordinates = false;
        int inZoneCount = 0;

        foreach (var candidate in routeCandidates)
        {
            // POI hoặc RoutePoi không active
            if (!candidate.IsPoiActive)
            {
                encounteredInactivePoi = true;
                continue;
            }

            if (!candidate.IsRoutePoiActive)
            {
                encounteredInactiveRoutePoi = true;
                continue;
            }

            if (candidate.PoiId == Guid.Empty)
            {
                continue;
            }

            // POI thiếu tọa độ
            if (!candidate.Latitude.HasValue || !candidate.Longitude.HasValue)
            {
                encounteredMissingPoiCoordinates = true;
                continue;
            }

            if (candidate.Latitude.Value is < -90m or > 90m || candidate.Longitude.Value is < -180m or > 180m)
            {
                encounteredInvalidPoiCoordinates = true;
                continue;
            }

            // Bán kính null hoặc không dương: KHÔNG tự đặt default, giữ hành vi không trigger
            if (!candidate.TriggerRadiusM.HasValue || candidate.TriggerRadiusM.Value <= 0)
            {
                encounteredInvalidRadius = true;
                continue;
            }

            // Kiểm tra geofence boundary bằng IGeoDistanceCalculator NTS
            bool isWithin = _distanceCalculator.IsWithinTriggerRadius(
                position.Latitude,
                position.Longitude,
                candidate.Latitude,
                candidate.Longitude,
                candidate.TriggerRadiusM);

            if (!isWithin)
            {
                continue;
            }

            inZoneCount++;

            // Tính khoảng cách chính xác theo mét
            double distanceMeters = _distanceCalculator.CalculateDistanceMeters(
                position.Latitude,
                position.Longitude,
                candidate.Latitude.Value,
                candidate.Longitude.Value);

            // Kiểm tra AudioGuide tiếng Việt đã publish (BR-EXP-02, BR-EXP-03)
            var publishedVietnameseAudio = candidate.AudioGuides.FirstOrDefault(a =>
                a.AudioGuideId != Guid.Empty && a.PoiId == candidate.PoiId &&
                a.IsPublished &&
                string.Equals(a.LanguageCode, "vi", StringComparison.OrdinalIgnoreCase) &&
                IsStableAssetIdentifier(a.AudioAssetUri));

            if (publishedVietnameseAudio == null)
            {
                encounteredMissingAudio = true;
                continue;
            }

            // Kiểm tra chống kích hoạt lặp lại trong cùng lượt ghé (BR-EXP-05)
            bool alreadyTriggered = await _deduplicationService.HasBeenTriggeredAsync(
                tripContext.TripId,
                DeduplicationId(candidate),
                tripContext.VisitId,
                cancellationToken);

            if (alreadyTriggered)
            {
                encounteredAlreadyTriggered = true;
                continue;
            }

            qualifiedMatches.Add(new QualifyingPoiMatch(
                candidate,
                publishedVietnameseAudio,
                distanceMeters));
        }

        // Không đoán tiến độ tuyến từ GPS khi các vùng POI chồng nhau.
        if (!tripContext.NextRoutePoiDisplayOrder.HasValue && inZoneCount > 1)
            return PoiAudioTriggerEvaluationResult.Skipped(
                TriggerDecisionReason.AmbiguousRouteProgress,
                "Nhiều vùng POI chồng nhau; cần thứ tự RoutePoi tiếp theo từ tiến độ hành trình.");

        // 9. Xử lý trường hợp không có ứng viên nào thỏa mãn
        if (qualifiedMatches.Count == 0)
        {
            if (encounteredAlreadyTriggered)
            {
                return PoiAudioTriggerEvaluationResult.Skipped(
                    TriggerDecisionReason.AlreadyTriggeredForVisit,
                    "POI trong vùng đã được kích hoạt trước đó cho lượt ghé hiện tại của chuyến tàu (BR-EXP-05 chống lặp).");
            }

            if (encounteredMissingAudio)
            {
                return PoiAudioTriggerEvaluationResult.Skipped(
                    TriggerDecisionReason.NoPublishedVietnameseAudio,
                    "POI nằm trong bán kính nhưng chưa có AudioGuide tiếng Việt ('vi') đã publish và có URI hợp lệ.");
            }

            if (encounteredInvalidRadius)
            {
                return PoiAudioTriggerEvaluationResult.Skipped(
                    TriggerDecisionReason.TriggerRadiusMissingOrInvalid,
                    "POI trên tuyến có TriggerRadiusM null hoặc không hợp lệ; hệ thống không tự đặt bán kính mặc định.");
            }

            if (encounteredInactivePoi)
            {
                return PoiAudioTriggerEvaluationResult.Skipped(
                    TriggerDecisionReason.PoiInactive,
                    "POI trên tuyến đang ở trạng thái không hoạt động.");
            }

            if (encounteredInactiveRoutePoi)
            {
                return PoiAudioTriggerEvaluationResult.Skipped(
                    TriggerDecisionReason.RoutePoiInactive,
                    "Liên kết RoutePoi trên tuyến đang ở trạng thái không hoạt động.");
            }

            if (encounteredInvalidPoiCoordinates)
                return PoiAudioTriggerEvaluationResult.Skipped(
                    TriggerDecisionReason.PoiCoordinatesInvalid, "Tọa độ POI không hợp lệ.");

            if (encounteredMissingPoiCoordinates)
                return PoiAudioTriggerEvaluationResult.Skipped(
                    TriggerDecisionReason.PoiCoordinatesMissing, "POI chưa có tọa độ.");

            return PoiAudioTriggerEvaluationResult.Skipped(
                TriggerDecisionReason.OutsideTriggerRadius,
                "Tàu hiện nằm ngoài bán kính kích hoạt của tất cả POI trên tuyến.");
        }

        // 10. Khi tiến độ đã xác định, chỉ xét RoutePoi tương ứng; không ưu tiên POI gần nhất.
        var bestMatch = qualifiedMatches
            .OrderBy(m => m.Candidate.DisplayOrder)
            .ThenBy(m => m.DistanceMeters)
            .First();

        var deduplicationId = DeduplicationId(bestMatch.Candidate);

        if (!await _deduplicationService.TryReserveAsync(
            tripContext.TripId, deduplicationId, tripContext.VisitId, cancellationToken))
            return PoiAudioTriggerEvaluationResult.Skipped(
                TriggerDecisionReason.AlreadyTriggeredForVisit,
                "POI này đã có yêu cầu trigger cho lượt ghé hiện tại.");

        var triggerRequest = new AudioTriggerRequest
        {
            RequestId = StableRequestId(tripContext.TripId, deduplicationId, tripContext.VisitId),
            TripId = tripContext.TripId,
            BoatId = tripContext.BoatId,
            RouteId = tripContext.RouteId,
            RoutePoiId = bestMatch.Candidate.RoutePoiId,
            RoutePoiDisplayOrder = bestMatch.Candidate.DisplayOrder,
            PoiId = bestMatch.Candidate.PoiId,
            PoiName = bestMatch.Candidate.PoiName,
            AudioGuideId = bestMatch.AudioGuide.AudioGuideId,
            AudioAssetUri = bestMatch.AudioGuide.AudioAssetUri,
            DurationSeconds = bestMatch.AudioGuide.DurationSeconds,
            DistanceMeters = bestMatch.DistanceMeters,
            TriggerRadiusMeters = bestMatch.Candidate.TriggerRadiusM!.Value,
            TriggeredAt = position.ObservedAt != default ? position.ObservedAt : DateTimeOffset.UtcNow,
            VisitId = tripContext.VisitId
        };

        // 11. Gửi yêu cầu phát tới Onboard Player (BR-EXP-03)
        AudioTriggerDispatchResult dispatchResult;
        try
        {
            dispatchResult = await _dispatcher.DispatchTriggerRequestAsync(triggerRequest, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            await _deduplicationService.ReleaseReservationAsync(
                tripContext.TripId, deduplicationId, tripContext.VisitId, CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            await _deduplicationService.ReleaseReservationAsync(
                tripContext.TripId, deduplicationId, tripContext.VisitId, CancellationToken.None);
            _logger?.LogWarning("Không dispatch được trigger audio ({ExceptionType}).", ex.GetType().Name);
            return PoiAudioTriggerEvaluationResult.Skipped(
                TriggerDecisionReason.DispatchFailed, "Không gửi được yêu cầu phát audio tới Onboard Player.");
        }

        if (!dispatchResult.IsDispatched || !dispatchResult.IsAcceptedByPlayer
            || dispatchResult.RequestId != triggerRequest.RequestId)
        {
            await _deduplicationService.ReleaseReservationAsync(
                tripContext.TripId, deduplicationId, tripContext.VisitId, CancellationToken.None);
            _logger?.LogWarning("Gửi yêu cầu phát audio cho POI {PoiId} trên Trip {TripId} thất bại.",
                bestMatch.Candidate.PoiId, tripContext.TripId);

            return PoiAudioTriggerEvaluationResult.Skipped(
                TriggerDecisionReason.DispatchFailed,
                "Gửi yêu cầu phát audio tới Onboard Player thất bại.");
        }

        // 12. Chỉ ghi nhận sau ACK player đã chấp nhận lệnh; chưa phải ACK bắt đầu phát.
        await _deduplicationService.MarkAsTriggeredAsync(
            tripContext.TripId,
            deduplicationId,
            tripContext.VisitId,
            CancellationToken.None);

        _logger?.LogInformation(
            "Đã phát lệnh trigger audio POI {PoiId} ({PoiName}) cho Trip {TripId} tại khoảng cách {Distance:F1}m (bán kính {Radius}m).",
            bestMatch.Candidate.PoiId, bestMatch.Candidate.PoiName, tripContext.TripId,
            bestMatch.DistanceMeters, bestMatch.Candidate.TriggerRadiusM.Value);

        return PoiAudioTriggerEvaluationResult.Triggered(
            bestMatch.Candidate.PoiId,
            bestMatch.Candidate.PoiName,
            bestMatch.AudioGuide.AudioGuideId,
            bestMatch.AudioGuide.AudioAssetUri,
            bestMatch.DistanceMeters,
            bestMatch.Candidate.TriggerRadiusM.Value,
            triggerRequest,
            dispatchResult);
    }

    private sealed record QualifyingPoiMatch(
        PoiGeofenceCandidate Candidate,
        AudioGuideCandidate AudioGuide,
        double DistanceMeters);

    private static Guid DeduplicationId(PoiGeofenceCandidate candidate) =>
        candidate.RoutePoiId == Guid.Empty ? candidate.PoiId : candidate.RoutePoiId;

    // Cùng lượt ghé retry dùng cùng RequestId để player có thể khử trùng lệnh đã nhận.
    private static Guid StableRequestId(Guid tripId, Guid routePoiId, string? visitId)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{tripId:N}:{routePoiId:N}:{(visitId is null ? "0" : "1:" + visitId)}"));
        return new Guid(bytes.AsSpan(0, 16));
    }

    private static bool IsStableAssetIdentifier(string? assetUri) =>
        !string.IsNullOrWhiteSpace(assetUri)
        && !assetUri.Contains('?') && !assetUri.Contains('#')
        && !assetUri.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
        && !assetUri.StartsWith('/') && !assetUri.StartsWith('~')
        && !(assetUri.Length >= 2 && char.IsLetter(assetUri[0]) && assetUri[1] == ':')
        && !assetUri.Contains('\\');
}
