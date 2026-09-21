namespace WaterbusSystem.Application.Common.Interfaces;

/// <summary>
/// Interface Quản lý Khóa phân tán (Distributed Lock) trên Redis
/// Đóng vai trò là LỚP PHÒNG THỦ 1 chống Double-booking / Overselling tại tầng RAM/Cache
/// </summary>
public interface IDistributedLockService
{
    /// <summary>
    /// Thử lấy distributed lock với khóa tài nguyên xác định
    /// </summary>
    /// <param name="resourceKey">Key định danh tài nguyên (ví dụ: lock:trip:{tripId}:seat:{seatId})</param>
    /// <param name="expiryTime">Thời gian giữ lock tối đa trước khi tự release (TTL)</param>
    /// <param name="waitTime">Thời gian chờ tối đa để lấy lock</param>
    /// <param name="retryTime">Chu kỳ thử lại giữa các lần cố gắng</param>
    /// <param name="cancellationToken">Token hủy tác vụ</param>
    /// <returns>Đối tượng IAsyncDisposable để giải phóng lock khi hoàn tất, hoặc null nếu không lấy được lock</returns>
    Task<IAsyncDisposable?> AcquireLockAsync(
        string resourceKey, 
        TimeSpan expiryTime, 
        TimeSpan waitTime, 
        TimeSpan retryTime, 
        CancellationToken cancellationToken = default);
}
