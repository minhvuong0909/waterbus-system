using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.SightseeingAudio;
using WaterbusSystem.Application.Features.SightseeingAudio.Interfaces;
using WaterbusSystem.Application.Features.SightseeingAudio.Models;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Application.Services.SightseeingAudio;

/// <summary>
/// Thông báo bến theo thứ tự ghé (<c>VisitOrder</c>). Tàu thường: bến đầu chỉ "rời bến", bến giữa "sắp tới → đã tới → rời bến",
/// bến cuối "sắp tới → đã tới". Tàu tham quan: "rời bến" ở bến đầu, "sắp tới" + "đã tới" ở bến cuối (không có bến giữa).
/// Mỗi lần gọi phát tối đa MỘT thông báo: sự kiện kế tiếp chưa phát nếu điều kiện khoảng cách thỏa mãn.
/// Chống trùng dùng chung sổ cái với POI: khóa = TripStopCallId, lượt ghé = tên sự kiện.
/// </summary>
public sealed class StationAnnouncementDecisionService(
    IGeoDistanceCalculator distance,
    IPoiTriggerDeduplicationService deduplication,
    IOnboardAudioTriggerDispatcher dispatcher,
    IOptions<SightseeingAudioOptions> options,
    ILogger<StationAnnouncementDecisionService> logger) : IStationAnnouncementDecisionService
{
    private enum Step { Approaching, Arrived, Departed }

    private sealed record StationEvent(TripStopInfo Stop, Step Step);

    public async Task<StationAnnouncementResult> EvaluateAndTriggerAsync(
        BoatPositionSnapshot position, TripStationContext context, CancellationToken ct = default)
    {
        var trip = context.Trip;
        if (position.BoatId != trip.BoatId || position.TripId != trip.TripId)
            return StationAnnouncementResult.Skipped("Vị trí không khớp Boat/Trip.");
        if (trip.Status != TripStatus.EnRoute)
            return StationAnnouncementResult.Skipped("Chuyến không ở trạng thái EnRoute.");
        if (trip.IsSafetyAnnouncementActive)
            return StationAnnouncementResult.Skipped("Đang có thông báo an toàn.");

        var cfg = options.Value;
        foreach (var ev in EventsOf(context))
        {
            var visit = ev.Step.ToString().ToLowerInvariant();
            if (await deduplication.HasBeenTriggeredAsync(trip.TripId, ev.Stop.TripStopCallId, visit, ct)) continue;

            // Sự kiện kế tiếp chưa phát: chưa tới điều kiện thì chờ (không nhảy cóc sang sự kiện sau).
            var meters = distance.CalculateDistanceMeters(position.Latitude, position.Longitude, ev.Stop.Latitude, ev.Stop.Longitude);
            var due = ev.Step switch
            {
                Step.Approaching => meters <= cfg.ApproachRadiusM,
                Step.Arrived => meters <= cfg.ArriveRadiusM,
                _ => meters > cfg.DepartRadiusM
            };
            if (!due) return StationAnnouncementResult.Skipped($"Chưa tới điều kiện {ev.Step} bến {ev.Stop.StationName}.");

            var clips = ev.Step switch
            {
                Step.Approaching => ev.Stop.ApproachingClips,
                Step.Arrived => ev.Stop.ArrivedClips,
                _ => ev.Stop.DepartedClips
            };
            return await TriggerAsync(position, trip, ev, visit, clips, meters, ct);
        }
        return StationAnnouncementResult.Skipped("Đã phát hết thông báo bến của chuyến.");
    }

    /// <summary>Chuỗi sự kiện theo thứ tự của cả chuyến.</summary>
    private static IEnumerable<StationEvent> EventsOf(TripStationContext context)
    {
        var stops = context.Stops.OrderBy(s => s.VisitOrder).ToList();
        var sightseeing = context.Trip.TripType == TripType.Sightseeing;
        for (var i = 0; i < stops.Count; i++)
        {
            var first = i == 0;
            var last = i == stops.Count - 1;
            if (sightseeing && !first && !last) continue;
            if (!first)
            {
                yield return new StationEvent(stops[i], Step.Approaching);
                yield return new StationEvent(stops[i], Step.Arrived);
            }
            if (!last) yield return new StationEvent(stops[i], Step.Departed);
        }
    }

    private async Task<StationAnnouncementResult> TriggerAsync(BoatPositionSnapshot position, TripSightseeingContext trip,
        StationEvent ev, string visit, IReadOnlyList<AudioClipRef> clips, double meters, CancellationToken ct)
    {
        var key = ev.Stop.TripStopCallId;
        if (clips.Count == 0)
        {
            logger.LogWarning("Bến {Station} chưa có clip {Step}; bỏ qua.", ev.Stop.StationName, ev.Step);
            return StationAnnouncementResult.Skipped("Thiếu clip audio.");
        }
        if (!await deduplication.TryReserveAsync(trip.TripId, key, visit, ct))
            return StationAnnouncementResult.Skipped("Đã có yêu cầu cho sự kiện này.");

        var request = new AudioTriggerRequest
        {
            RequestId = AudioRequestIds.For(trip.TripId, key, visit),
            TripId = trip.TripId,
            BoatId = trip.BoatId,
            RouteId = trip.RouteId,
            Kind = ev.Step switch
            {
                Step.Approaching => AudioClipKind.StationApproaching,
                Step.Arrived => AudioClipKind.StationArrived,
                _ => AudioClipKind.StationDeparted
            },
            SubjectId = ev.Stop.StationId,
            SubjectName = ev.Stop.StationName,
            Clips = clips,
            DurationSeconds = clips.Sum(c => c.DurationSeconds),
            DistanceMeters = meters,
            TriggeredAt = position.ObservedAt != default ? position.ObservedAt : DateTimeOffset.UtcNow,
            VisitId = visit,
            ReservationEpoch = deduplication.GetReservationEpoch(trip.TripId, key, visit)
        };

        AudioTriggerDispatchResult result;
        try
        {
            result = await dispatcher.DispatchTriggerRequestAsync(request, ct);
        }
        catch (Exception ex)
        {
            await deduplication.ReleaseReservationAsync(trip.TripId, key, visit, CancellationToken.None);
            if (ex is OperationCanceledException) throw;
            logger.LogWarning("Không gửi được thông báo bến ({ExceptionType}).", ex.GetType().Name);
            return StationAnnouncementResult.Skipped("Gửi thất bại.");
        }

        if (!result.IsDispatched || !result.IsAcceptedByPlayer || result.RequestId != request.RequestId)
        {
            if (result.Message != "ReservationLost")
                await deduplication.ReleaseReservationAsync(trip.TripId, key, visit, CancellationToken.None);
            return StationAnnouncementResult.Skipped("Player chưa nhận lệnh.");
        }

        await deduplication.MarkAsTriggeredAsync(trip.TripId, key, visit, CancellationToken.None);
        logger.LogInformation("Thông báo bến {Step} {Station} cho Trip {TripId} ({Meters:F0} m).",
            ev.Step, ev.Stop.StationName, trip.TripId, meters);
        return new StationAnnouncementResult(true, $"{ev.Step} {ev.Stop.StationName}");
    }
}
