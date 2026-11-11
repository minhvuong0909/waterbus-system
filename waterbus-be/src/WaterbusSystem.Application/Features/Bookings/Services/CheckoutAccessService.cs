using System.Security.Cryptography;
using System.Text;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Domain.Entities;

namespace WaterbusSystem.Application.Features.Bookings.Services;

public sealed class CheckoutAccessService(IAccessGrantRepository grants, CheckoutOptions options,
    ICurrentUserService? currentUser = null) : ICheckoutAccessService
{
    public const string ManageOrder = "ManageOrder";
    public const string PassengerTrip = "PassengerTrip";

    public string Issue(PurchaseOrder order, DateTimeOffset now) => IssueGrant(order.Id, order, null, ManageOrder, now).Token;

    /// <summary>Cấp grant mới (token ngẫu nhiên 256-bit, chỉ lưu hash) — BR-ID-02.</summary>
    public (string Token, DateTimeOffset ExpiresAt) IssueGrant(Guid orderId, PurchaseOrder? order, Guid? bookingId,
        string accessType, DateTimeOffset now)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var expiresAt = now.AddDays(Math.Clamp(options.AccessTokenTtlDays, 1, 365));
        grants.Add(new AccessGrant
        {
            Order = order, OrderId = orderId, BookingId = bookingId, AccessType = accessType,
            TokenHash = Hash(token), ExpiresAt = expiresAt
        });
        return (token, expiresAt);
    }

    /// <summary>Token Manage Order hợp lệ HOẶC Passenger đăng nhập là chủ sở hữu đã liên kết của Order.</summary>
    public async Task RequireAsync(Guid orderId, string? token, CancellationToken ct)
    {
        if (await IsOwnerAsync(orderId, ct)) return;
        await RequireTokenAsync(orderId, token, ct);
    }

    /// <summary>Chỉ chấp nhận token (dùng khi cần CHỨNG MINH quyền truy cập Order, vd liên kết tài khoản — BR-ID-04).</summary>
    public async Task RequireTokenAsync(Guid orderId, string? token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 128 ||
            !await grants.HasActiveGrantAsync(orderId, Hash(token), DateTimeOffset.UtcNow, ct))
            throw new ForbiddenAccessException("Không có quyền truy cập đơn hàng này.");
    }

    public async Task<bool> IsOwnerAsync(Guid orderId, CancellationToken ct) =>
        Guid.TryParse(currentUser?.UserId, out var accountId) && await grants.IsOrderOwnerAsync(orderId, accountId, ct);

    public Task<GuestAccessIdentity?> FindIdentityAsync(string? token, CancellationToken ct) =>
        string.IsNullOrWhiteSpace(token) || token.Length > 128
            ? Task.FromResult<GuestAccessIdentity?>(null)
            : grants.FindIdentityAsync(Hash(token), DateTimeOffset.UtcNow, ct);

    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}
