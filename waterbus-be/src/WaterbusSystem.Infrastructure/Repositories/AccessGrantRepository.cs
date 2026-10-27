using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Infrastructure.Persistence;

namespace WaterbusSystem.Infrastructure.Repositories;

public sealed class AccessGrantRepository(ApplicationDbContext context) : IAccessGrantRepository
{
    public void Add(AccessGrant grant) => context.AccessGrants.Add(grant);

    public Task<bool> HasActiveGrantAsync(Guid orderId, string tokenHash, DateTimeOffset now, CancellationToken ct) =>
        context.AccessGrants.AsNoTracking().AnyAsync(g => g.OrderId == orderId &&
            g.AccessType == "ManageOrder" && g.TokenHash == tokenHash && g.RevokedAt == null && g.ExpiresAt > now, ct);

    public async Task<GuestAccessIdentity?> FindIdentityAsync(string tokenHash, DateTimeOffset now, CancellationToken ct)
    {
        var grant = await context.AccessGrants.AsNoTracking()
            .Where(g => g.AccessType == "ManageOrder" && g.TokenHash == tokenHash && g.RevokedAt == null && g.ExpiresAt > now)
            .Select(g => new GuestAccessIdentity(g.OrderId, null)).FirstOrDefaultAsync(ct);
        return grant ?? await context.Bookings.AsNoTracking()
            .Where(b => !b.IsDeleted && b.ManageOrderTokenHash == tokenHash &&
                b.ManageOrderTokenExpiresAt > now && !b.ManageOrderTokenRevoked)
            .Select(b => new GuestAccessIdentity(b.Id, b.Id)).FirstOrDefaultAsync(ct);
    }
}
