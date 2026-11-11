using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Domain.Entities;

namespace WaterbusSystem.Application.Interfaces.Services;

public interface ICheckoutAccessService
{
    string Issue(PurchaseOrder order, DateTimeOffset now);
    Task RequireAsync(Guid orderId, string? token, CancellationToken ct);
    Task<GuestAccessIdentity?> FindIdentityAsync(string? token, CancellationToken ct);
    /// <summary>Chỉ chấp nhận token Manage Order, không chấp nhận JWT chủ sở hữu (BR-ID-04: chứng minh quyền).</summary>
    Task RequireTokenAsync(Guid orderId, string? token, CancellationToken ct);
    /// <summary>Passenger đang đăng nhập là chủ sở hữu đã liên kết của Order.</summary>
    Task<bool> IsOwnerAsync(Guid orderId, CancellationToken ct);
    (string Token, DateTimeOffset ExpiresAt) IssueGrant(Guid orderId, PurchaseOrder? order, Guid? bookingId,
        string accessType, DateTimeOffset now);
}
