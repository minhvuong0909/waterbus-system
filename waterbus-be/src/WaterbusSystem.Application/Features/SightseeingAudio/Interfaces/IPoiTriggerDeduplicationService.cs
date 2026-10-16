namespace WaterbusSystem.Application.Features.SightseeingAudio.Interfaces;

/// <summary>
/// Interface quản lý trạng thái chống kích hoạt lặp (deduplication)
/// cho cùng POI trong cùng lượt ghé của chuyến tàu (BR-EXP-05).
/// Tách riêng interface để cho phép triển khai qua MemoryCache, Redis Distributed Cache
/// hoặc Persistent State Store khi tích hợp hệ thống.
/// </summary>
public interface IPoiTriggerDeduplicationService
{
    /// <summary>
    /// Kiểm tra xem POI đã được kích hoạt trong lượt ghé này của chuyến tàu hay chưa.
    /// </summary>
    /// <param name="tripId">ID chuyến tàu.</param>
    /// <param name="poiId">ID điểm POI.</param>
    /// <param name="visitId">Định danh lượt ghé (tùy chọn; null nếu tuyến thông thường 1 lượt).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True nếu đã được kích hoạt trước đó; ngược lại false.</returns>
    Task<bool> HasBeenTriggeredAsync(
        Guid tripId,
        Guid poiId,
        string? visitId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Đặt giữ chỗ nguyên tử cho một lượt ghé. Chỉ một GPS evaluation đồng thời
    /// được quyền dispatch; false nếu đã giữ chỗ hoặc đã dispatch trước đó.
    /// </summary>
    Task<bool> TryReserveAsync(
        Guid tripId,
        Guid poiId,
        string? visitId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ghi nhận POI đã được kích hoạt thành công cho lượt ghé của chuyến tàu.
    /// </summary>
    /// <param name="tripId">ID chuyến tàu.</param>
    /// <param name="poiId">ID điểm POI.</param>
    /// <param name="visitId">Định danh lượt ghé (tùy chọn).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task MarkAsTriggeredAsync(
        Guid tripId,
        Guid poiId,
        string? visitId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Giải phóng giữ chỗ nếu dispatch chưa thành công; không xóa trạng thái đã dispatch.
    /// </summary>
    Task ReleaseReservationAsync(
        Guid tripId,
        Guid poiId,
        string? visitId = null,
        CancellationToken cancellationToken = default);
}
