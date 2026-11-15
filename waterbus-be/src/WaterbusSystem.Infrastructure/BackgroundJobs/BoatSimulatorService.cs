using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.SightseeingAudio;
using WaterbusSystem.Application.Features.Tracking;
using WaterbusSystem.Application.DTOs.Requests.Tracking;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.Infrastructure.Services.Tracking;

namespace WaterbusSystem.Infrastructure.BackgroundJobs;

/// <summary>
/// Giả lập GPS cho demo (BR-TRK-01). Mỗi chu kỳ, với mỗi Trip đang chạy có Boat, tính vị trí dọc các bến
/// theo VisitOrder và gửi qua CHÍNH IBoatTrackingService.RecordPositionAsync với Source = "Simulated" (cùng luồng với GPS thật).
///
/// Hỗ trợ cả 2 chế độ:
/// 1. Demo (UseSimulatedSegmentDuration = true): Tàu di chuyển từ bến A sang bến B trong SegmentDurationSeconds (mặc định 30s),
///    đến bến dừng DwellTimeSeconds (mặc định 3s, speed = 0, toạ độ đứng yên tại bến) rồi mới đi tiếp chặng sau.
///    - Rời bến (Departing): Bắt đầu xuất bến -> kích hoạt thuyết minh phong cảnh (Sightseeing).
///    - Gần tới bến (Near): Đạt NearThreshold (80%) -> phát thông báo "gần tới bến".
///    - Tới bến (Arrived): Cập bến (100%) -> phát thông báo "đã tới bến", dừng đỗ đón/trả khách.
/// 2. Giờ thực (UseSimulatedSegmentDuration = false): Nội suy theo tổng thời gian thực tế của chuyến (DepartureTime -> ArrivalTime).
///
/// Phân loại 2 loại chuyến:
/// - Commuter: Chỉ phát âm thanh trạm bến (Stop Announcement: rời bến, gần tới bến, đã tới bến).
/// - Sightseeing: Vừa phát âm thanh trạm bến, vừa kích hoạt audio thuyết minh phong cảnh/di tích qua geofence GPS.
/// </summary>
public class BoatSimulatorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<TrackingOptions> _options;
    private readonly IOptionsMonitor<StopAnnouncementOptions> _stopOptions;
    private readonly ILogger<BoatSimulatorService> _logger;

    /// <summary>
    /// Lưu trạng thái đã phát thông báo: Key = (TripId, SegmentIndex, CycleNumber), Value = (DepartingFired, NearFired, ArrivedFired).
    /// </summary>
    private readonly ConcurrentDictionary<(Guid TripId, int SegmentIndex, long CycleNumber), (bool DepartingFired, bool NearFired, bool ArrivedFired)>
        _announcementState = new();

    public BoatSimulatorService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<TrackingOptions> options,
        IOptionsMonitor<StopAnnouncementOptions> stopOptions,
        ILogger<BoatSimulatorService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _stopOptions = stopOptions;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var simulator = _options.CurrentValue.Simulator;
            if (simulator.Enabled)
            {
                try
                {
                    await TickAsync(simulator, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "[SIMULATOR] Lỗi khi giả lập vị trí tàu.");
                }
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, simulator.IntervalSeconds)), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Dừng service bình thường khi ứng dụng shutdown
            }
        }
    }

    private async Task TickAsync(BoatSimulatorOptions simulator, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var cache = scope.ServiceProvider.GetRequiredService<IBoatPositionCache>();
        var tracking = scope.ServiceProvider.GetRequiredService<IBoatTrackingService>();
        var dateTime = scope.ServiceProvider.GetRequiredService<IDateTimeService>();
        var stopPublisher = scope.ServiceProvider.GetService<IStopAnnouncementPublisher>();
        var now = dateTime.Now;

        var statuses = simulator.IncludeScheduledTrips
            ? new[] { TripStatus.Scheduled, TripStatus.Boarding, TripStatus.EnRoute, TripStatus.Suspended }
            : new[] { TripStatus.EnRoute, TripStatus.Suspended };

        var trips = await context.Trips.AsNoTracking()
            .Where(t => t.BoatId != null && !t.IsDeleted && statuses.Contains(t.Status))
            .Select(t => new
            {
                t.Id,
                BoatId = t.BoatId!.Value,
                t.Status,
                t.TripType,
                RouteCode = t.Route!.Code,
                Start = t.ActualDepartureTime ?? t.DepartureTime,
                t.ArrivalTime,
                Stops = t.TripStopCalls
                    .Where(c => !c.IsDeleted)
                    .OrderBy(c => c.VisitOrder)
                    .Select(c => new
                    {
                        StationId = c.RouteStop!.Station!.Id,
                        StationName = c.RouteStop.Station.Name,
                        c.RouteStop.Station.Latitude,
                        c.RouteStop.Station.Longitude
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        var stopOpts = _stopOptions.CurrentValue;

        // Mỗi tàu chỉ giả lập 1 chuyến (chuyến khởi hành gần nhất)
        foreach (var trip in trips.GroupBy(t => t.BoatId).Select(g => g.OrderBy(t => t.Start).First()))
        {
            IReadOnlyList<RoutePathInterpolator.GeoPoint> path = DemoRoutePaths.For(trip.RouteCode)
                ?? trip.Stops.Select(s => new RoutePathInterpolator.GeoPoint(s.Latitude, s.Longitude)).ToList();
            if (path.Count < 2 || trip.Stops.Count < 2) continue;

            decimal lat, lng;
            decimal? speed;
            int segmentIndex = 0;
            double segmentProgress = 0;
            long cycleNumber = 0;
            bool isDwelling = false;

            if (trip.Status == TripStatus.Suspended)
            {
                var last = await cache.GetAsync(trip.BoatId, cancellationToken);
                var hold = last is null ? path[0] : new RoutePathInterpolator.GeoPoint((double)last.Lat, (double)last.Lng);
                (lat, lng, speed) = ((decimal)hold.Latitude, (decimal)hold.Longitude, 0m);
            }
            else if (stopOpts.UseSimulatedSegmentDuration && stopOpts.SegmentDurationSeconds > 0)
            {
                // ==================== CHẾ ĐỘ DEMO GIẢ LẬP THEO ĐOẠN ====================
                // Mỗi đoạn kéo dài SegmentDurationSeconds (ví dụ 30s) + DwellTimeSeconds (ví dụ 3s đỗ bến)
                int segmentCount = trip.Stops.Count - 1;
                double segDuration = Math.Max(1.0, stopOpts.SegmentDurationSeconds);
                double dwellDuration = Math.Max(0.0, stopOpts.DwellTimeSeconds);
                double segTotalTime = segDuration + dwellDuration;
                double totalCycleTime = segmentCount * segTotalTime;

                var totalElapsed = (now - trip.Start).TotalSeconds;
                if (totalElapsed < 0) totalElapsed = 0;

                cycleNumber = (long)(totalElapsed / totalCycleTime);
                double elapsedInCycle = totalElapsed % totalCycleTime;

                segmentIndex = Math.Min((int)(elapsedInCycle / segTotalTime), segmentCount - 1);
                double timeInSeg = elapsedInCycle - (segmentIndex * segTotalTime);

                if (timeInSeg < segDuration)
                {
                    // ĐANG DI CHUYỂN TỪ BẾN i SANG BẾN i+1
                    segmentProgress = Math.Clamp(timeInSeg / segDuration, 0.0, 1.0);
                    double totalFraction = (segmentIndex + segmentProgress) / segmentCount;
                    var point = RoutePathInterpolator.PointAt(path, totalFraction);

                    lat = Math.Round((decimal)point.Latitude, 7);
                    lng = Math.Round((decimal)point.Longitude, 7);
                    speed = Math.Round((decimal)RoutePathInterpolator.AverageSpeedKnots(path, TimeSpan.FromSeconds(totalCycleTime)), 2);
                }
                else
                {
                    // ĐÃ TỚI VÀ ĐANG ĐỖ TẠI BẾN ĐÍCH i+1 (DWELLING)
                    isDwelling = true;
                    segmentProgress = 1.0;
                    var destStop = trip.Stops[segmentIndex + 1];
                    lat = Math.Round((decimal)destStop.Latitude, 7);
                    lng = Math.Round((decimal)destStop.Longitude, 7);
                    speed = 0m; // Tàu dừng cho khách lên xuống
                }
            }
            else
            {
                // ==================== CHẾ ĐỘ GIỜ THỰC THEO LỊCH TRÌNH ====================
                var duration = trip.ArrivalTime - trip.Start;
                if (duration <= TimeSpan.Zero) continue;

                var elapsed = (now - trip.Start).TotalSeconds % duration.TotalSeconds;
                if (elapsed < 0) elapsed += duration.TotalSeconds;
                double fraction = elapsed / duration.TotalSeconds;
                var point = RoutePathInterpolator.PointAt(path, fraction);

                lat = Math.Round((decimal)point.Latitude, 7);
                lng = Math.Round((decimal)point.Longitude, 7);
                speed = Math.Round((decimal)RoutePathInterpolator.AverageSpeedKnots(path, duration), 2);

                int segmentCount = trip.Stops.Count - 1;
                double scaled = fraction * segmentCount;
                segmentIndex = Math.Min((int)scaled, segmentCount - 1);
                segmentProgress = Math.Clamp(scaled - segmentIndex, 0.0, 1.0);
            }

            // Ghi nhận vị trí (luôn cùng luồng với GPS thật, cập nhật cache + phát SignalR boatPosition)
            // Nếu tripType == Sightseeing && EnableGpsTriggerEvaluation == true -> tự động kích hoạt audio POI
            await tracking.RecordPositionAsync(new RecordBoatPositionRequest(
                trip.BoatId, lat, lng, speed, now, BoatPositionSources.Simulated, ReportedByCaptainId: null),
                cancellationToken);

            // Phát Stop Announcement nếu có publisher và chuyến đang EnRoute (không Suspended)
            if (stopPublisher is not null && trip.Status != TripStatus.Suspended)
            {
                var fromStop = trip.Stops[segmentIndex];
                var destinationStop = trip.Stops[segmentIndex + 1];
                var tripTypeStr = trip.TripType.ToString();
                var stateKey = (trip.Id, segmentIndex, cycleNumber);
                var state = _announcementState.GetOrAdd(stateKey, _ => (false, false, false));

                // 1. Rời bến: Khi tàu bắt đầu di chuyển trong chặng (không phải đang đỗ) và chưa bắn departing
                if (!state.DepartingFired && !isDwelling)
                {
                    _announcementState[stateKey] = (true, state.NearFired, state.ArrivedFired);
                    _logger.LogInformation(
                        "[STOP-ANNOUNCE] Chuyến {TripId} ({TripType}) đoạn {Seg}: rời bến {StationName} đi {DestName}",
                        trip.Id, tripTypeStr, segmentIndex, fromStop.StationName, destinationStop.StationName);

                    await stopPublisher.PublishDepartingAsync(
                        trip.Id, trip.BoatId, fromStop.StationId, fromStop.StationName,
                        now, tripTypeStr, cancellationToken);
                }

                // 2. Gần tới bến: progress >= NearThreshold (chỉ bắn 1 lần)
                if (!state.NearFired && segmentProgress >= stopOpts.NearThreshold && !isDwelling)
                {
                    _announcementState[stateKey] = (state.DepartingFired, true, state.ArrivedFired);
                    _logger.LogInformation(
                        "[STOP-ANNOUNCE] Chuyến {TripId} ({TripType}) đoạn {Seg}: gần tới bến {StationName} (progress={Pct:P0})",
                        trip.Id, tripTypeStr, segmentIndex, destinationStop.StationName, segmentProgress);

                    await stopPublisher.PublishNearStopAsync(
                        trip.Id, trip.BoatId, destinationStop.StationId, destinationStop.StationName,
                        segmentProgress, now, tripTypeStr, cancellationToken);
                }

                // 3. Đã tới bến: progress = 1.0 (tàu cập bến / đỗ bến)
                if (!state.ArrivedFired && (segmentProgress >= 0.999 || isDwelling))
                {
                    _announcementState[stateKey] = (state.DepartingFired, state.NearFired, true);
                    _logger.LogInformation(
                        "[STOP-ANNOUNCE] Chuyến {TripId} ({TripType}) đoạn {Seg}: đã tới bến {StationName}",
                        trip.Id, tripTypeStr, segmentIndex, destinationStop.StationName);

                    await stopPublisher.PublishArrivedAsync(
                        trip.Id, trip.BoatId, destinationStop.StationId, destinationStop.StationName,
                        segmentProgress, now, tripTypeStr, cancellationToken);
                }

                // Dọn dẹp trạng thái các chu kỳ hoặc đoạn quá cũ
                if (_announcementState.Count > 100)
                {
                    var oldKeys = _announcementState.Keys
                        .Where(k => k.TripId == trip.Id && (k.CycleNumber < cycleNumber || k.SegmentIndex < segmentIndex - 2))
                        .ToList();
                    foreach (var k in oldKeys)
                    {
                        _announcementState.TryRemove(k, out _);
                    }
                }
            }
        }
    }
}
