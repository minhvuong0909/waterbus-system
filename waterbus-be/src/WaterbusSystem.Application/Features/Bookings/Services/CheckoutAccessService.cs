using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Domain.Entities;

namespace WaterbusSystem.Application.Features.Bookings.Services;

/// <summary>Quyền sở hữu checkout; không thay đổi module đăng nhập hay Guest middleware.</summary>
public sealed class CheckoutAccessService(IApplicationDbContext context, CheckoutOptions options)
{
    public string Issue(PurchaseOrder order, DateTimeOffset now)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        context.AccessGrants.Add(new AccessGrant
        {
            Order = order, OrderId = order.Id, AccessType = "ManageOrder",
            TokenHash = Hash(token), ExpiresAt = now.AddDays(Math.Clamp(options.AccessTokenTtlDays, 1, 365))
        });
        return token;
    }

    public async Task RequireAsync(Guid orderId, string? token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 128 ||
            !await context.AccessGrants.AsNoTracking().AnyAsync(g => g.OrderId == orderId &&
                g.AccessType == "ManageOrder" && g.TokenHash == Hash(token) &&
                g.RevokedAt == null && g.ExpiresAt > DateTimeOffset.UtcNow, ct))
            throw new ForbiddenAccessException("Không có quyền truy cập đơn hàng này.");
    }

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}
