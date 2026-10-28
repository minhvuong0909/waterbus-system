using WaterbusSystem.Domain.Entities;

namespace WaterbusSystem.Application.Interfaces.Repositories;

public record GuestAccessIdentity(Guid OrderId, Guid? BookingId);

public interface IAccessGrantRepository
{
    void Add(AccessGrant grant);
    Task<bool> HasActiveGrantAsync(Guid orderId, string tokenHash, DateTimeOffset now, CancellationToken ct);
    Task<GuestAccessIdentity?> FindIdentityAsync(string tokenHash, DateTimeOffset now, CancellationToken ct);
}
