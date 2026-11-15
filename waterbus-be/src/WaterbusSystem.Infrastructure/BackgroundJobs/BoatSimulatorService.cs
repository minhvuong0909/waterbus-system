using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaterbusSystem.Application.Common.Interfaces;
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
/// Thời gian chuyến được lặp vòng để demo không phụ thuộc giờ khởi hành thật.
/// Trip Suspended: tàu đứng yên, vẫn phát lại vị trí cuối (BR-INC-05).
/// </summary>
public class BoatSimulatorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<TrackingOptions> _options;
    private readonly ILogger<BoatSimulatorService> _logger;

    /// <summary>Thời điểm simulator thấy chuyến chuyển sang EnRoute (gốc thời gian của chuyến demo; Start không ghi ActualDepartureTime).</summary>
    private readonly Dictionary<Guid, DateTimeOffset> _demoStartedAt = new();

    public BoatSimulatorService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<TrackingOptions> options,
        ILogger<BoatSimulatorService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
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
        var now = scope.ServiceProvider.GetRequiredService<IDateTimeService>().Now;

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
                RouteCode = t.Route!.Code,
                Start = t.ActualDepartureTime ?? t.DepartureTime,
                t.ArrivalTime,
                Stops = t.TripStopCalls
                    .Where(c => !c.IsDeleted)
                    .OrderBy(c => c.VisitOrder)
                    .Select(c => new { c.RouteStop!.Station!.Latitude, c.RouteStop.Station.Longitude })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        // Mỗi tàu chỉ giả lập 1 chuyến (chuyến khởi hành gần nhất)
        foreach (var trip in trips.GroupBy(t => t.BoatId).Select(g => g
            .OrderBy(t => t.Status is TripStatus.EnRoute or TripStatus.Boarding or TripStatus.Suspended ? 0 : 1)
                // Cùng cách chọn chuyến với tracking (chuyến khởi hành muộn nhất đang chạy) để vị trí gắn đúng chuyến.
                .ThenByDescending(t => t.Start).First()))
        {
            // Đường đi dọc sông của tuyến nếu có (nối thẳng các bến sẽ cắt qua đất ở khúc cua), không thì nối các bến
            IReadOnlyList<RoutePathInterpolator.GeoPoint> path = DemoRoutePaths.For(trip.RouteCode)
                ?? trip.Stops.Select(s => new RoutePathInterpolator.GeoPoint(s.Latitude, s.Longitude)).ToList();
            if (path.Count < 2) continue;

            decimal lat, lng;
            decimal? speed;
            if (trip.Status == TripStatus.Suspended)
            {
                var last = await cache.GetAsync(trip.BoatId, cancellationToken);
                var hold = last is null ? path[0] : new RoutePathInterpolator.GeoPoint((double)last.Lat, (double)last.Lng);
                (lat, lng, speed) = ((decimal)hold.Latitude, (decimal)hold.Longitude, 0m);
            }
            else if (simulator.DemoTripSeconds > 0 && trip.Stops.Count >= 2)
            {
                // Chuyến demo ngắn: dwell tại mỗi bến + giảm tốc gần bến, lặp vòng (không phụ thuộc giờ chạy thật).
                var fractions = trip.Stops.Select(s =>
                    RoutePathInterpolator.FractionOf(path, new RoutePathInterpolator.GeoPoint(s.Latitude, s.Longitude))).ToList();
                fractions[0] = 0;
                fractions[^1] = 1;
                for (var i = 1; i < fractions.Count; i++) fractions[i] = Math.Max(fractions[i], fractions[i - 1]);
                // Chưa EnRoute: tàu chờ ở bến đầu; chuyến chỉ bắt đầu chạy từ lúc Captain bấm Start.
                double elapsed = 0;
                if (trip.Status == TripStatus.EnRoute)
                {
                    if (!_demoStartedAt.TryGetValue(trip.Id, out var startedAt)) _demoStartedAt[trip.Id] = startedAt = now;
                    elapsed = (now - startedAt).TotalSeconds;
                }
                else
                {
                    _demoStartedAt.Remove(trip.Id);
                }
                var (fraction, dwelling) = DemoTimeline.At(fractions, simulator.DemoTripSeconds, simulator.DemoDwellSeconds, elapsed);
                var point = RoutePathInterpolator.PointAt(path, fraction);
                lat = Math.Round((decimal)point.Latitude, 7);
                lng = Math.Round((decimal)point.Longitude, 7);
                // Tốc độ trung bình của vòng demo rất lớn (cả tuyến trong vài phút): chỉ báo giá trị hiển thị hợp lý, không vượt giới hạn validate (100 hải lý/giờ).
                speed = dwelling ? 0m : Math.Min(25m, Math.Round((decimal)RoutePathInterpolator.AverageSpeedKnots(path, TimeSpan.FromSeconds(simulator.DemoTripSeconds)), 2));
            }
            else
            {
                var duration = trip.ArrivalTime - trip.Start;
                if (duration <= TimeSpan.Zero) continue;

                var elapsed = (now - trip.Start).TotalSeconds % duration.TotalSeconds;
                if (elapsed < 0) elapsed += duration.TotalSeconds;
                var point = RoutePathInterpolator.PointAt(path, elapsed / duration.TotalSeconds);

                lat = Math.Round((decimal)point.Latitude, 7);
                lng = Math.Round((decimal)point.Longitude, 7);
                speed = Math.Round((decimal)RoutePathInterpolator.AverageSpeedKnots(path, duration), 2);
            }

            await tracking.RecordPositionAsync(new RecordBoatPositionRequest(
                trip.BoatId, lat, lng, speed, now, BoatPositionSources.Simulated, ReportedByCaptainId: null),
                cancellationToken);
        }
    }
}
