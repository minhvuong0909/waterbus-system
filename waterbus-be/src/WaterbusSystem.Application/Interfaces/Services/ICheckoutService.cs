using WaterbusSystem.Application.DTOs.Requests.Bookings;
using WaterbusSystem.Application.DTOs.Responses.Bookings;

namespace WaterbusSystem.Application.Interfaces.Services;

public interface ICheckoutService
{
    Task<CheckoutResponse> HoldAsync(CreateCheckoutHoldRequest request, CancellationToken cancellationToken = default);
    Task<bool> SubmitAsync(Guid orderId, SubmitCheckoutRequest request, string? accessToken, CancellationToken cancellationToken = default);
    Task<CheckoutResponse> CreateAsync(CreateCheckoutRequest request, CancellationToken cancellationToken = default);
    Task<bool> CancelAsync(Guid orderId, string? accessToken, CancellationToken cancellationToken = default);
    Task<decimal> UpdateSeatsAsync(Guid orderId, Guid bookingId, UpdateCheckoutSeatsRequest request, string? accessToken, CancellationToken cancellationToken = default);
}