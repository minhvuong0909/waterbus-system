using WaterbusSystem.Application.Common.Interfaces;

namespace WaterbusSystem.Application.Features.Incidents;

/// <summary>
/// Đánh giá một quan sát thời tiết có cần cảnh báo không và ở mức nào.
/// High: dông (mã 2xx) hoặc gió giật vượt ngưỡng; Medium: gió trung bình vượt ngưỡng.
/// </summary>
public static class WeatherAssessment
{
    public static string? Severity(WeatherObservation observation, WeatherOptions options)
    {
        var isThunderstorm = observation.ConditionCode is >= 200 and < 300;
        var gust = observation.WindGustMs ?? 0;

        if (isThunderstorm || gust >= options.GustThresholdMs)
        {
            return IncidentConstants.Severities.High;
        }

        if (observation.WindSpeedMs >= options.WindSpeedThresholdMs)
        {
            return IncidentConstants.Severities.Medium;
        }

        return null;
    }

    public static string Describe(string routeCode, WeatherObservation observation)
    {
        var gust = observation.WindGustMs is { } g ? $", gió giật {g * 3.6:0.#} km/h" : string.Empty;
        return $"Cảnh báo thời tiết tuyến {routeCode}: {observation.Description}, gió {observation.WindSpeedMs * 3.6:0.#} km/h{gust}. " +
               "Admin xem xét từng chuyến bị ảnh hưởng; hệ thống không tự đổi trạng thái chuyến.";
    }
}
