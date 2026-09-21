using WaterbusSystem.Domain.Common;
using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Domain.Entities;

/// <summary>
/// Thực thể Vé điện tử cấp cho từng hành khách trên từng ghế
/// </summary>
public class Ticket : BaseEntity
{
    public Guid BookingId { get; set; }
    public Booking? Booking { get; set; }

    public Guid TripId { get; set; }
    public Trip? Trip { get; set; }

    public Guid SeatId { get; set; }
    public Seat? Seat { get; set; }

    /// <summary>
    /// Mã vé duy nhất dùng để tra cứu hoặc in ấn (Ví dụ: TK98234123)
    /// </summary>
    public string TicketCode { get; set; } = string.Empty;

    /// <summary>
    /// Giá vé thực tế của ghế này (sau khi nhân hệ số)
    /// </summary>
    public decimal Price { get; set; }

    public string PassengerName { get; set; } = string.Empty;

    /// <summary>
    /// Trạng thái vé: Valid, CheckedIn, Cancelled, Refunded
    /// </summary>
    public TicketStatus Status { get; set; } = TicketStatus.Pending;

    /// <summary>
    /// Chuỗi khóa bí mật sinh mã Dynamic QR xoay vòng (TOTP QR)
    /// </summary>
    public string QrSeed { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Thời điểm nhân viên soát vé quét thành công tại bến
    /// </summary>
    public DateTimeOffset? CheckedInAt { get; set; }

    /// <summary>
    /// ID nhân viên đã thực hiện soát vé
    /// </summary>
    public Guid? CheckedInByStaffId { get; set; }
}
