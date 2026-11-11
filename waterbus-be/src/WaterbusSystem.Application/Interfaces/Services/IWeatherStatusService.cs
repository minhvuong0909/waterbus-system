using WaterbusSystem.Application.DTOs.Responses.Incidents;

namespace WaterbusSystem.Application.Interfaces.Services;

public interface IWeatherStatusService
{
    /// <summary>Thời tiết hiện tại của mọi tuyến đang hoạt động. Chỉ đọc, không tạo Incident.</summary>
    Task<List<RouteWeatherResponse>> GetCurrentAsync(CancellationToken cancellationToken = default);
}
