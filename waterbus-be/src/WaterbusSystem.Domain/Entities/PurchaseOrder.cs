using WaterbusSystem.Domain.Common;
using WaterbusSystem.Domain.Enums;

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
    public PurchaseMode PurchaseMode { get; set; } = PurchaseMode.OneWay;
    public OrderStatus Status { get; set; } = OrderStatus.Draft;
    public decimal QuotedTotal { get; set; }
    public string Currency { get; set; } = "VND";
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<FinancialTransaction> FinancialTransactions { get; set; } = new List<FinancialTransaction>();

    /// <summary>Call at checkout submission; a Draft may still be incomplete.</summary>
    public void ValidateForCheckout()
    {
        if (!Enum.IsDefined(PurchaseMode))
            throw new InvalidOperationException("Unsupported purchase mode.");
        var expected = PurchaseMode == PurchaseMode.OneWay ? 1 : 2;
        if (Bookings.Count != expected || Bookings.Any(b => b.OrderId != Id))
            throw new InvalidOperationException("An Order must own one Booking for OneWay or two for RoundTrip.");
        if (string.IsNullOrWhiteSpace(PurchaserName) || string.IsNullOrWhiteSpace(PurchaserEmail))
            throw new InvalidOperationException("Purchaser name and email are required.");
        if (QuotedTotal != Bookings.Sum(b => b.TotalAmount))
            throw new InvalidOperationException("Order total must equal its Booking allocations.");
    }
}
