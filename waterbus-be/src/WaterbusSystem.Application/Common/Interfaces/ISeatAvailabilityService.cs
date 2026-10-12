namespace WaterbusSystem.Application.Common.Interfaces;

/// <summary>
/// Domain / Application Service giải quyết bài toán tái sử dụng ghế trên cùng một chuyến đi (Segment Overlap)
/// </summary>
public interface ISeatAvailabilityService
{
    /// <summary>
    /// Kiểm tra tính khả dụng của một ghế trên chuyến tàu trong khoảng chặng [requestedBoardingOrder, requestedDisembarkingOrder)
    /// </summary>
    /// <param name="tripId">Mã chuyến tàu</param>
    /// <param name="requestedSeatId">Mã ghế cần kiểm tra</param>
    /// <param name="requestedBoardingOrder">Thứ tự bến đón (1-indexed)</param>
    /// <param name="requestedDisembarkingOrder">Thứ tự bến trả (1-indexed)</param>
    /// <param name="cancellationToken">Token hủy thao tác</param>
    /// <returns>True nếu ghế còn trống (không bị đụng độ chặng), False nếu đã bị đặt</returns>
    Task<bool> IsSeatAvailableAsync(
        Guid tripId,
        Guid requestedSeatId,
        int requestedBoardingOrder,
        int requestedDisembarkingOrder,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lọc danh sách các ghế khả dụng trong tập hợp ghế được yêu cầu
    /// </summary>
    Task<List<Guid>> GetAvailableSeatsAsync(
        Guid tripId,
        IEnumerable<Guid> seatIds,
        int requestedBoardingOrder,
        int requestedDisembarkingOrder,
        CancellationToken cancellationToken = default);
}
