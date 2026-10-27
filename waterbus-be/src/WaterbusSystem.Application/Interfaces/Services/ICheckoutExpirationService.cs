namespace WaterbusSystem.Application.Interfaces.Services;

public interface ICheckoutExpirationService
{
    Task<int> ExpireAsync(CancellationToken ct = default);
}
