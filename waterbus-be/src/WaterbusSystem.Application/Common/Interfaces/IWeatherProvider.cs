namespace WaterbusSystem.Application.Common.Interfaces;

/// <summary>
/// Nguồn dữ liệu thời tiết hiện tại tại một toạ độ (OpenWeatherMap, hoặc Fake khi demo/test).
/// Lỗi mạng/dịch vụ ngoài được ném ra để Weather Guard tự quyết định bỏ qua điểm đó.
/// </summary>
public interface IWeatherProvider
{
    Task<WeatherObservation> GetCurrentAsync(double latitude, double longitude, CancellationToken cancellationToken = default);
}

/// <param name="WindSpeedMs">Tốc độ gió trung bình (m/s).</param>
/// <param name="WindGustMs">Gió giật (m/s), có thể không có.</param>
/// <param name="ConditionCode">Mã điều kiện thời tiết theo OpenWeatherMap (2xx = dông).</param>
public record WeatherObservation(
    double WindSpeedMs,
    double? WindGustMs,
    int ConditionCode,
    string Description,
    DateTimeOffset ObservedAt);
