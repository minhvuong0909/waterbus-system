using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Infrastructure.Services;

/// <summary>
/// Triển khai thuật toán kiểm tra đụng độ chặng di chuyển (Segment Overlap Algorithm)
/// Quy tắc nghiệp vụ: Một ghế có thể được bán lại trên cùng một chuyến tàu (Trip)
/// nếu và chỉ nếu khoảng chặng đón/trả mới [requestedBoardingOrder, requestedDisembarkingOrder)
/// không giao thoa với bất kỳ khoảng chặng [BoardingStopOrder, DisembarkingStopOrder) của đơn đặt chỗ nào còn hiệu lực.
/// </summary>
public class SeatAvailabilityService : ISeatAvailabilityService
{
    private readonly IApplicationDbContext _context;

    public SeatAvailabilityService(IApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Kiểm tra tính khả dụng của một ghế
    /// Điều kiện 2 khoảng [A, B) và [C, D) giao thoa nhau là: A < D VÀ B > C
    /// Trong đó:
    ///   A = requestedBoardingOrder
    ///   B = requestedDisembarkingOrder
    ///   C = r.BoardingStopOrder
    ///   D = r.DisembarkingStopOrder
    /// </summary>
    public async Task<bool> IsSeatAvailableAsync(
        Guid tripId,
        Guid requestedSeatId,
        int requestedBoardingOrder,
        int requestedDisembarkingOrder,
        CancellationToken cancellationToken = default)
    {
        if (requestedBoardingOrder >= requestedDisembarkingOrder)
        {
            throw new ArgumentException("Thứ tự bến đón phải nhỏ hơn thứ tự bến xuống.");
        }

        // Truy vấn tối ưu dịch thẳng sang SQL Server với mệnh đề IF NOT EXISTS
        var hasOverlap = await _context.SeatReservations
            .AsNoTracking()
            .AnyAsync(r => r.TripId == tripId
                        && r.SeatId == requestedSeatId
                        && r.Status != ReservationStatus.Cancelled
                        && requestedBoardingOrder < r.DisembarkingStopOrder
                        && requestedDisembarkingOrder > r.BoardingStopOrder,
                      cancellationToken);

        return !hasOverlap;
    }

    /// <summary>
    /// Lọc danh sách các ghế khả dụng cho nhiều ghế cùng lúc
    /// </summary>
    public async Task<List<Guid>> GetAvailableSeatsAsync(
        Guid tripId,
        IEnumerable<Guid> seatIds,
        int requestedBoardingOrder,
        int requestedDisembarkingOrder,
        CancellationToken cancellationToken = default)
    {
        if (requestedBoardingOrder >= requestedDisembarkingOrder)
        {
            throw new ArgumentException("Thứ tự bến đón phải nhỏ hơn thứ tự bến xuống.");
        }

        var seatIdList = seatIds.Distinct().ToList();
        if (seatIdList.Count == 0)
        {
            return new List<Guid>();
        }

        // Tìm các ghế bị đụng độ chặng
        var conflictingSeatIds = await _context.SeatReservations
            .AsNoTracking()
            .Where(r => r.TripId == tripId
                     && seatIdList.Contains(r.SeatId)
                     && r.Status != ReservationStatus.Cancelled
                     && requestedBoardingOrder < r.DisembarkingStopOrder
                     && requestedDisembarkingOrder > r.BoardingStopOrder)
            .Select(r => r.SeatId)
            .Distinct()
            .ToListAsync(cancellationToken);

        // Các ghế khả dụng là các ghế không nằm trong danh sách đụng độ
        return seatIdList.Except(conflictingSeatIds).ToList();
    }
}
