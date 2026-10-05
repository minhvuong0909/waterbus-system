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
    /// <summary>Service entitlement lifecycle; separate from check-in and refund progress.</summary>
    public TicketStatus Status { get; set; } = TicketStatus.Valid;
    public BoardingStatus BoardingStatus { get; set; } = BoardingStatus.NotCheckedIn;
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

    /// <summary>BR-COM-08: issue only from a verified successful Payment and its confirmed reservation.</summary>
    public static Ticket Issue(Booking booking, SeatReservation reservation, FinancialTransaction payment,
        TripType fareTripType, DateTimeOffset issuedAt)
    {
        if (payment.TransactionType != FinancialTransactionType.Payment ||
            payment.Status != FinancialTransactionStatus.Succeeded || payment.OrderId != booking.OrderId ||
            booking.Status != BookingStatus.Confirmed || !booking.PaidAllocation.HasValue ||
            reservation.BookingId != booking.Id || reservation.TripId != booking.TripId ||
            reservation.Status != ReservationStatus.Confirmed || !reservation.PaidAllocation.HasValue)
            throw new InvalidOperationException("Ticket issuance requires verified payment and a confirmed Booking/reservation.");
        if (reservation.Ticket != null || string.IsNullOrWhiteSpace(reservation.PassengerName) ||
            string.IsNullOrWhiteSpace(reservation.SeatClassAtSale) || string.IsNullOrWhiteSpace(reservation.SeatCodeAtSale) ||
            reservation.PaidAllocation < 0 || reservation.QuotedFare < 0 ||
            reservation.PaidAllocation > booking.PaidAllocation || booking.PaidAllocation > payment.Amount)
            throw new InvalidOperationException("Reservation must have valid passenger, seat and paid fare snapshots, with no existing Ticket.");

        var ticket = new Ticket
        {
            BookingId = booking.Id, Booking = booking,
            SeatReservationId = reservation.Id, SeatReservation = reservation,
            TicketCode = CodeGenerator.GenerateTicketCode(issuedAt),
            FareTripTypeSnapshot = fareTripType.ToString(), FareSeatClassSnapshot = reservation.SeatClassAtSale,
            FaceFareSnapshot = reservation.QuotedFare, PaidFareSnapshot = reservation.PaidAllocation.Value,
            IssuedAt = issuedAt
        };
        reservation.Ticket = ticket;
        return ticket;
    }
}
