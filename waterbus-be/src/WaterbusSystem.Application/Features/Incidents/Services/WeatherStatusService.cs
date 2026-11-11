using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Responses.Incidents;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Application.Interfaces.Services;

namespace WaterbusSystem.Application.Features.Incidents.Services;

public sealed class WeatherStatusService(
    IRouteRepository routes,
    IWeatherProvider weather,
    IOptions<WeatherOptions> options,
    ILogger<WeatherStatusService> logger) : IWeatherStatusService
{
    private readonly WeatherOptions _options = options.Value;

    public async Task<List<RouteWeatherResponse>> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        var activeRoutes = await routes.ListActiveWithStationPointsAsync(cancellationToken);

        // Mỗi toạ độ (làm tròn ~1 km) chỉ gọi dịch vụ ngoài 1 lần trong một lượt.
        var cache = new Dictionary<(double, double), WeatherObservation?>();
        var result = new List<RouteWeatherResponse>(activeRoutes.Count);

        foreach (var route in activeRoutes)
        {
            var points = route.RoundedDistinctPoints();
            WeatherObservation? worst = null;
            var failed = 0;

            foreach (var point in points)
            {
                if (!cache.TryGetValue(point, out var observation))
                {
                    observation = await TryObserveAsync(point.Lat, point.Lng, cancellationToken);
                    cache[point] = observation;
                }

                if (observation is null)
                {
                    failed++;
                    continue;
                }

                if (worst is null || IsWorse(observation, worst)) worst = observation;
            }

            result.Add(new RouteWeatherResponse(
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

    //Mức độ cao hơn thì tệ hơn; cùng mức thì gió giật rồi gió trung bình lớn hơn là tệ hơn.
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
            return await weather.GetCurrentAsync(lat, lng, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // Lỗi dịch vụ ngoài chỉ ghi log; tuyến đó hiện "Unknown" thay vì làm hỏng cả thẻ.
            logger.LogWarning(ex, "[WEATHER NOW] Không lấy được thời tiết tại ({Lat}, {Lng}).", lat, lng);
            return null;
        }
    }
}