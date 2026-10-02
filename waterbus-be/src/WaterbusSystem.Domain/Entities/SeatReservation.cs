using WaterbusSystem.Domain.Common;
using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Domain.Entities;

/// <summary>
/// Thực thể phân đoạn giữ chỗ ghế (Segment-based Seat Reservation)
/// Hỗ trợ bài toán tái sử dụng ghế trên cùng một chuyến đi qua nhiều chặng dừng
/// </summary>
public class SeatReservation : BaseEntity
{
    public Guid TripId { get; set; }
    public Trip? Trip { get; set; }

    public Guid SeatId { get; set; }
    public Seat? Seat { get; set; }

    public Guid? BookingId { get; set; }
    public Booking? Booking { get; set; }

    /// <summary>
    /// Thứ tự bến lên tàu (1-indexed order của bến đón khách)
    /// </summary>
    public int BoardingStopOrder { get; set; }

    /// <summary>
    /// Thứ tự bến xuống tàu (1-indexed order của bến trả khách)
    /// </summary>
    public int DisembarkingStopOrder { get; set; }

    /// <summary>
    /// Trạng thái đặt chỗ: Pending, Confirmed, Cancelled, Completed
    /// </summary>
    public ReservationStatus Status { get; set; } = ReservationStatus.Pending;
}
