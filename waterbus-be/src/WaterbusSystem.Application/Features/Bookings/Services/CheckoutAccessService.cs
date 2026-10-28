using System.Security.Cryptography;
using System.Text;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Domain.Entities;

namespace WaterbusSystem.Application.Features.Bookings.Services;

public sealed class CheckoutAccessService(IAccessGrantRepository grants, CheckoutOptions options) : ICheckoutAccessService
{
    public string Issue(PurchaseOrder order, DateTimeOffset now)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        grants.Add(new AccessGrant
        {
            Order = order, OrderId = order.Id, AccessType = "ManageOrder",
            TokenHash = Hash(token), ExpiresAt = now.AddDays(Math.Clamp(options.AccessTokenTtlDays, 1, 365))
        });
        return token;
    }

    public async Task RequireAsync(Guid orderId, string? token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 128 ||
            !await grants.HasActiveGrantAsync(orderId, Hash(token), DateTimeOffset.UtcNow, ct))
            throw new ForbiddenAccessException("Không có quyền truy cập đơn hàng này.");
    }

    public Task<GuestAccessIdentity?> FindIdentityAsync(string? token, CancellationToken ct) =>
        string.IsNullOrWhiteSpace(token) || token.Length > 128
            ? Task.FromResult<GuestAccessIdentity?>(null)
            : grants.FindIdentityAsync(Hash(token), DateTimeOffset.UtcNow, ct);

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}
