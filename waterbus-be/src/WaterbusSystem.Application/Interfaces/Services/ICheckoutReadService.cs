using WaterbusSystem.Application.DTOs.Responses.Bookings;

namespace WaterbusSystem.Application.Interfaces.Services;

public interface ICheckoutReadService
{
    Task<CheckoutDetailDto> GetAsync(Guid orderId, string? accessToken, CancellationToken ct = default);
}
