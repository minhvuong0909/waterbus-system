using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Tracking;
using WaterbusSystem.Application.Features.Tracking.Commands.RecordBoatPosition;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.Infrastructure.Services.Tracking;

namespace WaterbusSystem.Infrastructure.BackgroundJobs;

/// <summary>
/// Giả lập GPS cho demo (BR-TRK-01). Mỗi chu kỳ, với mỗi Trip đang chạy có Boat, tính vị trí dọc các bến
/// theo VisitOrder và gửi qua CHÍNH RecordBoatPositionCommand với Source = "Simulated" (cùng luồng với GPS thật).
/// Thời gian chuyến được lặp vòng để demo không phụ thuộc giờ khởi hành thật.
/// Trip Suspended: tàu đứng yên, vẫn phát lại vị trí cuối (BR-INC-05).
/// </summary>
public class BoatSimulatorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<TrackingOptions> _options;
    private readonly ILogger<BoatSimulatorService> _logger;

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
        var mediator = scope.ServiceProvider.GetRequiredService<ISender>();
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
        foreach (var trip in trips.GroupBy(t => t.BoatId).Select(g => g.OrderBy(t => t.Start).First()))
        {
            var path = trip.Stops
                .Select(s => new RoutePathInterpolator.GeoPoint(s.Latitude, s.Longitude))
                .ToList();
            if (path.Count < 2) continue;

            decimal lat, lng;
            decimal? speed;
            if (trip.Status == TripStatus.Suspended)
            {
                var last = await cache.GetAsync(trip.BoatId, cancellationToken);
                var hold = last is null ? path[0] : new RoutePathInterpolator.GeoPoint((double)last.Lat, (double)last.Lng);
                (lat, lng, speed) = ((decimal)hold.Latitude, (decimal)hold.Longitude, 0m);
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

            await mediator.Send(new RecordBoatPositionCommand(
                trip.BoatId, lat, lng, speed, now, BoatPositionSources.Simulated, ReportedByCaptainId: null),
                cancellationToken);
        }
    }
}
