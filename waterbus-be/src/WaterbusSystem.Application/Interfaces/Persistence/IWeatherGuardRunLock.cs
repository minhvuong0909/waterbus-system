namespace WaterbusSystem.Application.Interfaces.Persistence;

/// <summary>
/// Loại trừ lẫn nhau giữa các lượt quét thời tiết (theo lịch Hangfire và nút "Kiểm tra ngay"), kể cả giữa nhiều instance API.
/// Thiếu khoá này, hai lượt cùng thấy "chưa có cảnh báo" rồi cùng tạo Incident cho một tuyến.
/// </summary>
public interface IWeatherGuardRunLock
{
    /// <returns>Lease để giải phóng khoá, hoặc null nếu hết thời gian chờ mà lượt khác vẫn đang chạy.</returns>
    Task<IAsyncDisposable?> TryAcquireAsync(TimeSpan wait, CancellationToken cancellationToken = default);
}
