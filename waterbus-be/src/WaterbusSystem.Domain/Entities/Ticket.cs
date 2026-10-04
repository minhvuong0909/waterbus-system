using WaterbusSystem.Domain.Common;
using WaterbusSystem.Domain.Enums;
namespace WaterbusSystem.Domain.Entities;
/// <summary>
/// Vé điện tử — chỉ được phát hành SAU KHI thanh toán thành công (IPN verified).
/// Mỗi Ticket tương ứng 1:1 với 1 SeatReservation.
/// QR là per-Booking (không phải per-Ticket) — xem Booking.PublicBookingId.
/// </summary>
public class Ticket : BaseEntity
{
    public Guid BookingId { get; set; }
    public Booking? Booking { get; set; }
    /// <summary>
    /// FK 1:1 tới SeatReservation — xác định ghế và chặng đi của hành khách này.
    /// </summary>
    public Guid SeatReservationId { get; set; }
    public SeatReservation? SeatReservation { get; set; }
    /// <summary>Mã vé hiển thị (ví dụ: TK20260920ABCDEF)</summary>
    public string TicketCode { get; set; } = string.Empty;
    /// <summary>Trạng thái vé: Pending, Valid, Cancelled, Refunded</summary>
    public TicketStatus Status { get; set; } = TicketStatus.Pending;
    /// <summary>Trạng thái lên tàu: NotBoarded, CheckedIn, NoShow</summary>
    public string BoardingStatus { get; set; } = "NotBoarded";
    // ── Snapshot giá tại thời điểm mua (bất biến, không thay đổi khi bảng giá đổi) ──
    /// <summary>Loại chuyến lúc bán: "Commuter" hoặc "Sightseeing"</summary>
    public string FareTripTypeSnapshot { get; set; } = string.Empty;
    /// <summary>Hạng ghế lúc bán: "FrontCabin", "Standard", "Outdoor"</summary>
    public string FareSeatClassSnapshot { get; set; } = string.Empty;
    /// <summary>Giá niêm yết tại thời điểm mua (VNĐ)</summary>
    public decimal FaceFareSnapshot { get; set; }
    /// <summary>Giá thực trả sau khuyến mãi (VNĐ)</summary>
    public decimal PaidFareSnapshot { get; set; }
    /// <summary>Thời điểm Ticket được phát hành (sau IPN success)</summary>
    public DateTimeOffset? IssuedAt { get; set; }
    /// <summary>Thời điểm check-in thành công</summary>
    public DateTimeOffset? CheckedInAt { get; set; }
    /// <summary>Thời điểm xác định NoShow (tính theo cửa sổ check-in của bến lên)</summary>
    public DateTimeOffset? NoShowFinalizedAt { get; set; }
    /// <summary>ID nhân viên thực hiện check-in</summary>
    public Guid? CheckedInByStaffId { get; set; }
}
