using WaterbusSystem.Domain.Common;

namespace WaterbusSystem.Domain.Entities;

/// <summary>
/// Đơn hàng của người mua. 1 Order = 1 hoặc 2 Booking (OneWay/RoundTrip).
/// Người mua có thể là Passenger đăng nhập hoặc Guest (PassengerAccountId = null).
/// </summary>
public class PurchaseOrder : BaseEntity
{
    public Guid? PassengerAccountId { get; set; }     // NULL nếu Guest
    public string PurchaserName { get; set; } = string.Empty;
    public string PurchaserEmail { get; set; } = string.Empty;
    public string? PurchaserPhone { get; set; }
    public string PurchaseMode { get; set; } = "OneWay";   // "OneWay" hoặc "RoundTrip"
    public string Status { get; set; } = "Pending";         // "Pending", "Confirmed", "Cancelled"
    public decimal QuotedTotal { get; set; }
    public string Currency { get; set; } = "VND";
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
