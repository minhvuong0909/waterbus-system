using System.ComponentModel.DataAnnotations.Schema;
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

    /// <summary>One Booking represents one Trip and two distinct visits on that Trip.</summary>
    public Guid TripId { get; set; }
    public Trip? Trip { get; set; }
    public Guid BoardingCallId { get; set; }
    public TripStopCall? BoardingCall { get; set; }
    public Guid DisembarkingCallId { get; set; }
    public TripStopCall? DisembarkingCall { get; set; }

    // Public identifier is independent of purchaser data and the manage-order token.
    public string PublicBookingId { get; set; } = Guid.NewGuid().ToString("N");
    public int QrCredentialVersion { get; set; } = 1;
    public DateTimeOffset? QrIssuedAt { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string? CustomerPhone { get; set; }

    /// <summary>
    /// Giá báo tại checkout (VNĐ); tiền đã thu nằm ở PaidAllocation.
    /// </summary>
    public decimal QuotedTotal { get; set; }

    /// <summary>Compatibility alias for the existing API DTOs; persisted as QuotedTotal.</summary>
    [NotMapped]
    public decimal TotalAmount { get => QuotedTotal; set => QuotedTotal = value; }

    /// <summary>Actual amount collected for this Booking; null until verified payment.</summary>
    public decimal? PaidAllocation { get; set; }

    /// <summary>
    /// Vòng đời dịch vụ: Draft, PendingPayment, Confirmed, Cancelled, Terminated, Completed.
    /// </summary>
    public BookingStatus Status { get; set; } = BookingStatus.Draft;

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
    /// Legacy payment records retained for the existing integration. New Payment/Refund records belong to Order.
    /// </summary>
    public ICollection<PaymentTransaction> PaymentTransactions { get; set; } = new List<PaymentTransaction>();

    public void SetJourney(TripStopCall boarding, TripStopCall disembarking)
    {
        if (boarding.TripId == Guid.Empty || boarding.TripId != disembarking.TripId)
            throw new ArgumentException("Both boarding calls must belong to the same Trip.");
        if (boarding.Id == disembarking.Id || boarding.VisitOrder >= disembarking.VisitOrder)
            throw new ArgumentException("Disembarking must follow boarding in the Trip visit order.");
        if (boarding.VisitOrder <= 0 || boarding.Status == "Skipped" || disembarking.Status == "Skipped" ||
            boarding.RouteStop?.CanBoard == false || disembarking.RouteStop?.CanAlight == false)
            throw new ArgumentException("The selected calls must permit boarding and disembarking.");

        TripId = boarding.TripId;
        BoardingCallId = boarding.Id;
        BoardingCall = boarding;
        DisembarkingCallId = disembarking.Id;
        DisembarkingCall = disembarking;
    }
}
