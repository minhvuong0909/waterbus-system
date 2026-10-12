using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Application.Features.Incidents.Commands.RunWeatherGuard;

public record WeatherGuardResult(int RoutesChecked, int IncidentsCreated, int IncidentsUpdated, int ObservationFailures);

/// <summary>
/// Quét thời tiết tại các bến của những Route có Trip sắp/đang chạy và ghi nhận Incident thời tiết.
/// - BR-INC-01/02: một Incident phạm vi Route, liên kết các Trip bị ảnh hưởng qua IncidentTrip.
/// - BR-INC-03/04: KHÔNG đổi TripStatus; OperationDecision để trống cho Admin quyết định.
/// - Idempotent: Incident Open cùng Route trong AlertWindowHours được cập nhật, không tạo trùng.
/// </summary>
public record RunWeatherGuardCommand : IRequest<WeatherGuardResult>;

public class RunWeatherGuardCommandHandler : IRequestHandler<RunWeatherGuardCommand, WeatherGuardResult>
{
    private static readonly TripStatus[] ActiveTripStatuses =
        [TripStatus.Scheduled, TripStatus.Boarding, TripStatus.EnRoute, TripStatus.Suspended];

    private readonly IApplicationDbContext _context;
    private readonly IWeatherProvider _weather;
    private readonly IOperationsAlertPublisher _alerts;
    private readonly IDateTimeService _dateTime;
    private readonly WeatherOptions _options;
    private readonly ILogger<RunWeatherGuardCommandHandler> _logger;

    public RunWeatherGuardCommandHandler(
        IApplicationDbContext context,
        IWeatherProvider weather,
        IOperationsAlertPublisher alerts,
        IDateTimeService dateTime,
        IOptions<WeatherOptions> options,
        ILogger<RunWeatherGuardCommandHandler> logger)
    {
        _context = context;
        _weather = weather;
        _alerts = alerts;
        _dateTime = dateTime;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<WeatherGuardResult> Handle(RunWeatherGuardCommand request, CancellationToken cancellationToken)
    {
        var now = _dateTime.Now;
        var horizon = now.AddHours(_options.LookaheadHours);

        // Trip chưa kết thúc, đang chạy hoặc khởi hành trong khung nhìn trước
        var trips = await _context.Trips.AsNoTracking()
            .Where(t => !t.IsDeleted
                        && ActiveTripStatuses.Contains(t.Status)
                        && t.DepartureTime <= horizon
                        && t.ArrivalTime >= now)
            .Select(t => new { t.Id, t.RouteId })
            .ToListAsync(cancellationToken);

        var tripsByRoute = trips.GroupBy(t => t.RouteId).ToDictionary(g => g.Key, g => g.Select(t => t.Id).ToList());
        if (tripsByRoute.Count == 0)
        {
            return new WeatherGuardResult(0, 0, 0, 0);
        }

        var routes = await _context.Routes.AsNoTracking()
            .Where(r => tripsByRoute.Keys.Contains(r.Id) && !r.IsDeleted)
            .Select(r => new
            {
                r.Id,
                r.Code,
                Stops = r.Stops.Where(s => !s.IsDeleted).Select(s => new { s.Station!.Latitude, s.Station.Longitude }).ToList(),
                Departure = new { r.DepartureStation!.Latitude, r.DepartureStation.Longitude },
                Arrival = new { r.ArrivalStation!.Latitude, r.ArrivalStation.Longitude }
            })
            .ToListAsync(cancellationToken);

        // Nhiều tuyến dùng chung bến: mỗi toạ độ (làm tròn ~1 km) chỉ gọi API 1 lần trong 1 lượt quét
        var observationCache = new Dictionary<(double, double), WeatherObservation?>();
        int created = 0, updated = 0, failures = 0;

        foreach (var route in routes)
        {
            var points = route.Stops.Count > 0
                ? route.Stops.Select(s => (s.Latitude, s.Longitude))
                : new[] { (route.Departure.Latitude, route.Departure.Longitude), (route.Arrival.Latitude, route.Arrival.Longitude) };

            WeatherObservation? worst = null;
            string? worstSeverity = null;

            foreach (var (lat, lng) in points.Select(p => (Math.Round(p.Item1, 2), Math.Round(p.Item2, 2))).Distinct())
            {
                if (!observationCache.TryGetValue((lat, lng), out var observation))
                {
                    observation = await TryObserveAsync(lat, lng, cancellationToken);
                    if (observation is null) failures++;
                    observationCache[(lat, lng)] = observation;
                }

                if (observation is null) continue;

                var severity = WeatherAssessment.Severity(observation, _options);
                if (IncidentConstants.Severities.Rank(severity) > IncidentConstants.Severities.Rank(worstSeverity))
                {
                    worst = observation;
                    worstSeverity = severity;
                }
            }

            if (worst is null || worstSeverity is null) continue;

            var isNew = await RecordIncidentAsync(route.Id, route.Code, worst, worstSeverity, tripsByRoute[route.Id], now, cancellationToken);
            if (isNew) created++; else updated++;
        }

        _logger.LogInformation(
            "[WEATHER GUARD] Đã kiểm tra {Routes} tuyến: tạo {Created}, cập nhật {Updated} Incident, {Failures} lần lấy thời tiết thất bại.",
            routes.Count, created, updated, failures);

        return new WeatherGuardResult(routes.Count, created, updated, failures);
    }

    private async Task<WeatherObservation?> TryObserveAsync(double lat, double lng, CancellationToken cancellationToken)
    {
        try
        {
            return await _weather.GetCurrentAsync(lat, lng, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // Lỗi dịch vụ ngoài chỉ ghi log, không làm hỏng cả lượt quét
            _logger.LogWarning(ex, "[WEATHER GUARD] Không lấy được thời tiết tại ({Lat}, {Lng}).", lat, lng);
            return null;
        }
    }

    /// <returns>true nếu tạo Incident mới, false nếu cập nhật Incident đang mở.</returns>
    private async Task<bool> RecordIncidentAsync(
        Guid routeId, string routeCode, WeatherObservation observation, string severity,
        List<Guid> affectedTripIds, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var windowStart = now.AddHours(-_options.AlertWindowHours);
        var description = WeatherAssessment.Describe(routeCode, observation);

        var incident = await _context.Incidents
            .Include(i => i.AffectedTrips)
            .Where(i => !i.IsDeleted
                        && i.RouteId == routeId
                        && i.Source == IncidentConstants.Sources.WeatherService
                        && i.IncidentType == IncidentConstants.Types.Weather
                        && i.Status == IncidentConstants.Statuses.Open
                        && i.OccurredAt >= windowStart)
            .OrderByDescending(i => i.OccurredAt)
            .FirstOrDefaultAsync(cancellationToken);

        var isNew = incident is null;
        var escalated = false;

        if (incident is null)
        {
            incident = new Incident
            {
                ScopeType = IncidentConstants.Scopes.Route,
                RouteId = routeId,
                Source = IncidentConstants.Sources.WeatherService,
                IncidentType = IncidentConstants.Types.Weather,
                Severity = severity,
                Status = IncidentConstants.Statuses.Open,
                Description = description,
                OccurredAt = now
            };
            _context.Incidents.Add(incident);
        }
        else
        {
            escalated = IncidentConstants.Severities.Rank(severity) > IncidentConstants.Severities.Rank(incident.Severity);
            if (escalated) incident.Severity = severity;
            incident.Description = description;
            incident.UpdatedAt = now;
        }

        // Chỉ liên kết thêm Trip mới bị ảnh hưởng; quyết định vận hành để trống cho Admin
        var linked = incident.AffectedTrips.Select(t => t.TripId).ToHashSet();
        foreach (var tripId in affectedTripIds.Where(id => !linked.Contains(id)))
        {
            _context.IncidentTrips.Add(new IncidentTrip { IncidentId = incident.Id, TripId = tripId });
        }

        await _context.SaveChangesAsync(cancellationToken);

        if (isNew || escalated)
        {
            await _alerts.PublishWeatherAlertAsync(
                new WeatherAlertMessage(incident.Id, incident.Severity, routeId, description, now), cancellationToken);
        }

        return isNew;
    }
}
