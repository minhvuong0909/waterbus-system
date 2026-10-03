using WaterbusSystem.Domain.Common;
using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Domain.Entities;

/// <summary>
/// Đơn đặt chỗ (Booking) của khách hàng
/// </summary>
public class Booking : BaseEntity
{
    /// <summary>
    /// Mã đơn đặt hiển thị (Ví dụ: WB20260920153045987)
    /// </summary>
    public Guid OrderId { get; set; }
    public PurchaseOrder? Order { get; set; }

    public string BookingCode { get; set; } = string.Empty;

    // Public ID dùng trong QR (non-sensitive, có thể expose)
    public string PublicBookingId { get; set; } = string.Empty;
    public int QrCredentialVersion { get; set; } = 1;
    public DateTimeOffset? QrIssuedAt { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;

    /// <summary>
    /// Tổng số tiền thanh toán (VNĐ)
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// Trạng thái đơn đặt: Pending, Confirmed, Cancelled, Refunded
    /// </summary>
    public BookingStatus Status { get; set; } = BookingStatus.Pending;

    /// <summary>
    /// Trạng thái thanh toán: Pending, Success, Failed, Refunded
    /// </summary>
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

    public string? Notes { get; set; }

    /// <summary>
    /// Mã băm bảo mật dùng để xác thực quyền quản lý đơn hàng của khách vãng lai (Guest Access qua header X-Guest-Token)
    /// </summary>
    public string? ManageOrderTokenHash { get; set; }

    /// <summary>
    /// Thời điểm hết hạn của ManageOrderToken (NULL = chưa có token)
    /// </summary>
    public DateTimeOffset? ManageOrderTokenExpiresAt { get; set; }

    /// <summary>
    /// Token đã bị thu hồi (revoke) hay chưa
    /// </summary>
    public bool ManageOrderTokenRevoked { get; set; } = false;

    /// <summary>
    /// Danh sách các vé thuộc đơn đặt này
    /// </summary>
    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();

    public ICollection<SeatReservation> SeatReservations { get; set; } = new List<SeatReservation>();

    /// <summary>
    /// Lịch sử các giao dịch thanh toán qua cổng VNPAY/MoMo
    /// </summary>
    public ICollection<PaymentTransaction> PaymentTransactions { get; set; } = new List<PaymentTransaction>();
}
