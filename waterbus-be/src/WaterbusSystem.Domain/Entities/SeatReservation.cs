using WaterbusSystem.Domain.Common;
using WaterbusSystem.Domain.Enums;
namespace WaterbusSystem.Domain.Entities;
/// <summary>
/// Giữ chỗ theo chặng cho 1 hành khách trên 1 ghế trong 1 Booking.
/// Stop orders are snapshots of Booking.BoardingCall/DisembarkingCall.VisitOrder,
/// never the global Station.OrderIndex. All reservations share the Booking journey.
/// </summary>
public class SeatReservation : BaseEntity
{
    public Guid TripId { get; set; }
    public Trip? Trip { get; set; }
    public Guid SeatId { get; set; }
    public Seat? Seat { get; set; }
    public Guid BookingId { get; set; }
    public Booking? Booking { get; set; }
    // Segment snapshots used by the overlap query.
    public int BoardingStopOrder { get; set; }
    public int DisembarkingStopOrder { get; set; }
    /// <summary>Trạng thái: Pending, Confirmed, Cancelled, Completed</summary>
    public ReservationStatus Status { get; set; } = ReservationStatus.Pending;
    // ── Thông tin hành khách (người thực sự ngồi ghế này, có thể khác Purchaser) ──
    public string PassengerName { get; set; } = string.Empty;
    public string? PassengerEmail { get; set; }
    public string? PassengerPhone { get; set; }
    // ── Snapshot tài chính ──
    /// <summary>Giá niêm yết tại thời điểm book</summary>
    public decimal QuotedFare { get; set; }
    /// <summary>Phần tiền thực trả cho chỗ này (sau IPN)</summary>
    public decimal? PaidAllocation { get; set; }
    // ── Snapshot ghế lúc bán (phục vụ Boat replacement) ──
    /// <summary>Mã ghế tại thời điểm bán (ví dụ: "S01")</summary>
    public string? SeatCodeAtSale { get; set; }
    /// <summary>Hạng ghế tại thời điểm bán (ví dụ: "Standard")</summary>
    public string? SeatClassAtSale { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    /// <summary>Navigation 1:1 → Ticket (được tạo sau khi thanh toán)</summary>
    public Ticket? Ticket { get; set; }
}
