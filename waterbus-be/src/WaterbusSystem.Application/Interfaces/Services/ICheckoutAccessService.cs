using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Domain.Entities;

namespace WaterbusSystem.Application.Interfaces.Services;

public interface ICheckoutAccessService
{
    string Issue(PurchaseOrder order, DateTimeOffset now);
    Task RequireAsync(Guid orderId, string? token, CancellationToken ct);
    Task<GuestAccessIdentity?> FindIdentityAsync(string? token, CancellationToken ct);
}
