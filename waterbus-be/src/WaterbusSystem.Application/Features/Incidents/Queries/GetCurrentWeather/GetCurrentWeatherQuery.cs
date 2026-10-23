using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaterbusSystem.Application.Common.Interfaces;

namespace WaterbusSystem.Application.Features.Incidents.Queries.GetCurrentWeather;

/// <summary>Trạng thái thời tiết hiện tại của một tuyến, theo cùng ngưỡng với Weather Guard.</summary>
public static class RouteWeatherStatus
{
    /// <summary>Dưới mọi ngưỡng cảnh báo.</summary>
    public const string Normal = "Normal";
    /// <summary>Gió trung bình vượt ngưỡng (Incident mức Medium).</summary>
    public const string Caution = "Caution";
    /// <summary>Dông hoặc gió giật vượt ngưỡng (Incident mức High).</summary>
    public const string Danger = "Danger";
    /// <summary>Không lấy được thời tiết ở bến nào của tuyến.</summary>
    public const string Unknown = "Unknown";
}

/// <summary>
/// Thời tiết hiện tại của một tuyến: quan sát tệ nhất trong các bến của tuyến.
/// Tốc độ gió tính bằng m/s như <see cref="WeatherObservation"/>; FE tự đổi sang km/h.
/// </summary>
public record RouteWeatherDto(
    Guid RouteId,
    string RouteCode,
    string RouteName,
    string Status,
    double? WindSpeedMs,
    double? WindGustMs,
    int? ConditionCode,
    string? Description,
    DateTimeOffset? ObservedAt,
    int PointsChecked,
    int PointsFailed);

/// <summary>
/// Đọc thời tiết hiện tại của mọi tuyến đang hoạt động (kể cả khi không có cảnh báo) cho thẻ
/// "Thời tiết hiện tại" của Admin. CHỈ ĐỌC: không tạo/cập nhật Incident, không đổi Trip (BR-INC-03);
/// việc ghi nhận cảnh báo vẫn do Weather Guard (<c>RunWeatherGuardCommand</c>) làm.
/// </summary>
public record GetCurrentWeatherQuery : IRequest<List<RouteWeatherDto>>;

public class GetCurrentWeatherQueryHandler : IRequestHandler<GetCurrentWeatherQuery, List<RouteWeatherDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IWeatherProvider _weather;
    private readonly WeatherOptions _options;
    private readonly ILogger<GetCurrentWeatherQueryHandler> _logger;

    public GetCurrentWeatherQueryHandler(
        IApplicationDbContext context,
        IWeatherProvider weather,
        IOptions<WeatherOptions> options,
        ILogger<GetCurrentWeatherQueryHandler> logger)
    {
        _context = context;
        _weather = weather;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<List<RouteWeatherDto>> Handle(GetCurrentWeatherQuery request, CancellationToken cancellationToken)
    {
        var routes = await _context.Routes.AsNoTracking()
            .Where(r => !r.IsDeleted && r.IsActive)
            .OrderBy(r => r.Code)
            .Select(r => new
            {
                r.Id,
                r.Code,
                r.Name,
                Stops = r.Stops.Where(s => !s.IsDeleted).Select(s => new { s.Station!.Latitude, s.Station.Longitude }).ToList(),
                Departure = new { r.DepartureStation!.Latitude, r.DepartureStation.Longitude },
                Arrival = new { r.ArrivalStation!.Latitude, r.ArrivalStation.Longitude }
            })
            .ToListAsync(cancellationToken);

        // Cùng cách lấy điểm như Weather Guard: mỗi toạ độ (làm tròn ~1 km) chỉ gọi dịch vụ ngoài 1 lần.
        var cache = new Dictionary<(double, double), WeatherObservation?>();
        var result = new List<RouteWeatherDto>(routes.Count);

        foreach (var route in routes)
        {
            var points = (route.Stops.Count > 0
                    ? route.Stops.Select(s => (s.Latitude, s.Longitude))
                    : new[] { (route.Departure.Latitude, route.Departure.Longitude), (route.Arrival.Latitude, route.Arrival.Longitude) })
                .Select(p => (Math.Round(p.Item1, 2), Math.Round(p.Item2, 2)))
                .Distinct()
                .ToList();

            WeatherObservation? worst = null;
            var failed = 0;
            foreach (var point in points)
            {
                if (!cache.TryGetValue(point, out var observation))
                {
                    observation = await TryObserveAsync(point.Item1, point.Item2, cancellationToken);
                    cache[point] = observation;
                }

                if (observation is null)
                {
                    failed++;
                    continue;
                }

                if (worst is null || IsWorse(observation, worst)) worst = observation;
            }

            result.Add(new RouteWeatherDto(
                route.Id, route.Code, route.Name,
                worst is null ? RouteWeatherStatus.Unknown : StatusOf(worst),
                worst?.WindSpeedMs, worst?.WindGustMs, worst?.ConditionCode, worst?.Description, worst?.ObservedAt,
                points.Count, failed));
        }

        return result;
    }

    private string StatusOf(WeatherObservation observation) => WeatherAssessment.Severity(observation, _options) switch
    {
        IncidentConstants.Severities.High => RouteWeatherStatus.Danger,
        IncidentConstants.Severities.Medium => RouteWeatherStatus.Caution,
        _ => RouteWeatherStatus.Normal
    };

    /// <summary>Mức độ cao hơn thì tệ hơn; cùng mức thì gió giật rồi gió trung bình lớn hơn là tệ hơn.</summary>
    private bool IsWorse(WeatherObservation candidate, WeatherObservation current)
    {
        var rank = IncidentConstants.Severities.Rank(WeatherAssessment.Severity(candidate, _options))
                   - IncidentConstants.Severities.Rank(WeatherAssessment.Severity(current, _options));
        if (rank != 0) return rank > 0;
        var gust = (candidate.WindGustMs ?? 0) - (current.WindGustMs ?? 0);
        return gust != 0 ? gust > 0 : candidate.WindSpeedMs > current.WindSpeedMs;
    }

    private async Task<WeatherObservation?> TryObserveAsync(double lat, double lng, CancellationToken cancellationToken)
    {
        try
        {
            return await _weather.GetCurrentAsync(lat, lng, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // Lỗi dịch vụ ngoài chỉ ghi log; tuyến đó hiện "Unknown" thay vì làm hỏng cả thẻ.
            _logger.LogWarning(ex, "[WEATHER NOW] Không lấy được thời tiết tại ({Lat}, {Lng}).", lat, lng);
            return null;
        }
    }
}
