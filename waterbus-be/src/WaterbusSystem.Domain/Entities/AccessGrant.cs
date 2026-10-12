using WaterbusSystem.Domain.Common;
namespace WaterbusSystem.Domain.Entities;
/// <summary>
/// Token truy cập an toàn cho Guest — thay thế ManageOrderTokenHash trực tiếp trên Booking.
/// Lưu hash SHA-256, KHÔNG lưu raw token.
/// AccessType: "ManageOrder" (quản lý đơn) hoặc "PassengerTrip" (xem thông tin chuyến).
/// </summary>
public class AccessGrant : BaseEntity
{
    public Guid OrderId { get; set; }
    public PurchaseOrder? Order { get; set; }
    /// <summary>Chỉ set cho AccessType = "PassengerTrip"</summary>
    public Guid? BookingId { get; set; }
    public Booking? Booking { get; set; }
    /// <summary>"ManageOrder" hoặc "PassengerTrip"</summary>
    public string AccessType { get; set; } = "ManageOrder";
    /// <summary>SHA-256 hash của raw token — UNIQUE, KHÔNG lưu raw</summary>
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
}
