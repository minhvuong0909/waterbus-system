using WaterbusSystem.Application.DTOs.Responses.Incidents;

namespace WaterbusSystem.Application.Interfaces.Services;

public interface IWeatherGuardService
{
    /// <summary>Quét thời tiết các tuyến có chuyến sắp/đang chạy, ghi và tự đóng Incident thời tiết.</summary>
    Task<WeatherGuardResponse> RunAsync(CancellationToken cancellationToken = default);
}
