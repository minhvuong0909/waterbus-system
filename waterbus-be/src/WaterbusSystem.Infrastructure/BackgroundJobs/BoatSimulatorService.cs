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
/// Dịch vụ nền giả lập vị trí GPS tàu và phát thông báo trạm bến theo hành trình.
/// </summary>
public class BoatSimulatorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<TrackingOptions> _options;
    private readonly IStopAnnouncementConfigStore _stopConfig;
    private readonly ILogger<BoatSimulatorService> _logger;

    public BoatSimulatorService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<TrackingOptions> options,
        IStopAnnouncementConfigStore stopConfig,
        ILogger<BoatSimulatorService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _stopConfig = stopConfig;
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
                break;
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
        var announcements = scope.ServiceProvider.GetService<IStopAnnouncementService>();
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
                t.ActualDepartureTime,
                RouteCode = t.Route!.Code,
                Start = t.ActualDepartureTime ?? t.DepartureTime,
                t.ArrivalTime,
                Stops = t.TripStopCalls
                    .Where(c => !c.IsDeleted)
                    .OrderBy(c => c.VisitOrder)
                    .Select(c => new
                    {
                        c.VisitOrder,
                        StationId = c.RouteStop!.Station!.Id,
                        StationName = c.RouteStop.Station.Name,
                        c.RouteStop.Station.Latitude,
                        c.RouteStop.Station.Longitude
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        var stopOpts = _stopConfig.Current;

        foreach (var trip in trips.GroupBy(t => t.BoatId).Select(g => g
            .OrderBy(t => t.Status == TripStatus.EnRoute ? 0 : t.Status == TripStatus.Suspended ? 1 : 2)
            .ThenByDescending(t => t.ActualDepartureTime ?? DateTimeOffset.MinValue)
            .ThenBy(t => t.Start)
            .First()))
        {
            IReadOnlyList<RoutePathInterpolator.GeoPoint> path = DemoRoutePaths.For(trip.RouteCode)
                ?? trip.Stops.Select(s => new RoutePathInterpolator.GeoPoint(s.Latitude, s.Longitude)).ToList();
            if (path.Count < 2 || trip.Stops.Count < 2) continue;

            decimal lat, lng;
            decimal? speed;
            int segmentIndex = 0;
            double segmentProgress = 0;
            bool isDwelling = false;
            double totalElapsed = 0;
            double totalCycleTime = double.MaxValue;

            if (trip.Status == TripStatus.Suspended)
            {
                var last = await cache.GetAsync(trip.BoatId, cancellationToken);
                var hold = last is null ? path[0] : new RoutePathInterpolator.GeoPoint((double)last.Lat, (double)last.Lng);
                (lat, lng, speed) = ((decimal)hold.Latitude, (decimal)hold.Longitude, 0m);
            }
            else if (stopOpts.UseSimulatedSegmentDuration && stopOpts.TripDurationSeconds > 0)
            {
                int segmentCount = Math.Max(1, trip.Stops.Count - 1);
                double dwellDuration = Math.Max(0, stopOpts.DwellTimeSeconds);
                // TripDurationSeconds là TỔNG thời gian cả chuyến (bến đầu → bến cuối), gồm cả thời gian đậu.
                // Chia đều phần thời gian di chuyển cho các chặng; phần đậu cộng thêm vào mỗi chặng.
                double movingBudget = Math.Max(1, stopOpts.TripDurationSeconds - (segmentCount * dwellDuration));
                double segDuration = movingBudget / segmentCount;
                double segTotalTime = segDuration + dwellDuration;
                totalCycleTime = segmentCount * segTotalTime;

                totalElapsed = (now - trip.Start).TotalSeconds;
                if (totalElapsed < 0) totalElapsed = 0;

                if (totalElapsed >= totalCycleTime)
                {
                    isDwelling = true;
                    segmentIndex = segmentCount - 1;
                    segmentProgress = 1.0;
                    var lastStop = trip.Stops[segmentCount];
                    lat = Math.Round((decimal)lastStop.Latitude, 7);
                    lng = Math.Round((decimal)lastStop.Longitude, 7);
                    speed = 0m;
                }
                else
                {
                    segmentIndex = Math.Min((int)(totalElapsed / segTotalTime), segmentCount - 1);
                    double timeInSeg = totalElapsed - (segmentIndex * segTotalTime);

                    if (timeInSeg < segDuration)
                    {
                        segmentProgress = Math.Clamp(timeInSeg / segDuration, 0.0, 1.0);
                        double totalFraction = (segmentIndex + segmentProgress) / segmentCount;
                        var point = RoutePathInterpolator.PointAt(path, totalFraction);

                        lat = Math.Round((decimal)point.Latitude, 7);
                        lng = Math.Round((decimal)point.Longitude, 7);
                        speed = 12.5m;
                    }
                    else
                    {
                        isDwelling = true;
                        segmentProgress = 1.0;
                        var destStop = trip.Stops[segmentIndex + 1];
                        lat = Math.Round((decimal)destStop.Latitude, 7);
                        lng = Math.Round((decimal)destStop.Longitude, 7);
                        speed = 0m;
                    }
                }
            }
            else
            {
                var duration = trip.ArrivalTime - trip.Start;
                if (duration <= TimeSpan.Zero) continue;

                totalElapsed = (now - trip.Start).TotalSeconds;
                totalCycleTime = duration.TotalSeconds;
                var elapsed = totalElapsed % duration.TotalSeconds;
                if (elapsed < 0) elapsed += duration.TotalSeconds;
                double fraction = elapsed / duration.TotalSeconds;
                var point = RoutePathInterpolator.PointAt(path, fraction);

                lat = Math.Round((decimal)point.Latitude, 7);
                lng = Math.Round((decimal)point.Longitude, 7);
                speed = Math.Round((decimal)Math.Min(50.0, RoutePathInterpolator.AverageSpeedKnots(path, duration)), 2);

                int segmentCount = trip.Stops.Count - 1;
                double scaled = fraction * segmentCount;
                segmentIndex = Math.Min((int)scaled, segmentCount - 1);
                segmentProgress = Math.Clamp(scaled - segmentIndex, 0.0, 1.0);
            }

            await tracking.RecordPositionAsync(new RecordBoatPositionRequest(
                trip.BoatId, lat, lng, speed, now, BoatPositionSources.Simulated, ReportedByCaptainId: null),
                cancellationToken);

            if (announcements is null || trip.Status != TripStatus.EnRoute) continue;
            if (totalElapsed > totalCycleTime + 2.0) continue;

            var fromStop = trip.Stops[segmentIndex];
            var destinationStop = trip.Stops[segmentIndex + 1];

            if (!isDwelling)
            {
                await announcements.AnnounceDepartingAsync(trip.Id, fromStop.VisitOrder, cancellationToken);

                if (segmentProgress >= stopOpts.NearThreshold)
                {
                    await announcements.AnnounceNearStopAsync(
                        trip.Id, destinationStop.VisitOrder, segmentProgress, cancellationToken);
                }
            }
            else
            {
                await announcements.AnnounceArrivedAsync(
                    trip.Id, destinationStop.VisitOrder, segmentProgress, cancellationToken);
            }
        }
    }
}
