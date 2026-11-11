using WaterbusSystem.Domain.Entities;

namespace WaterbusSystem.Application.Interfaces.Repositories;

public record GuestAccessIdentity(Guid OrderId, Guid? BookingId);

public interface IAccessGrantRepository
{
    void Add(AccessGrant grant);
    Task<bool> HasActiveGrantAsync(Guid orderId, string tokenHash, DateTimeOffset now, CancellationToken ct);
    Task<GuestAccessIdentity?> FindIdentityAsync(string tokenHash, DateTimeOffset now, CancellationToken ct);
    /// <summary>Chủ sở hữu đã liên kết (PurchaseOrder.PassengerAccountId) — truy cập bằng JWT thay cho token.</summary>
    Task<bool> IsOrderOwnerAsync(Guid orderId, Guid accountId, CancellationToken ct);
    /// <summary>Grant còn hiệu lực, được theo dõi để thu hồi trong cùng transaction.</summary>
    Task<AccessGrant?> GetActiveTrackedAsync(Guid orderId, string tokenHash, string accessType, DateTimeOffset now, CancellationToken ct);
    Task<List<AccessGrant>> GetActiveForOrderTrackedAsync(Guid orderId, DateTimeOffset now, CancellationToken ct);
    /// <summary>Tra TripAccess token → Booking được phép xem.</summary>
    Task<Guid?> FindTripAccessBookingAsync(string tokenHash, DateTimeOffset now, CancellationToken ct);
}
