using WaterbusSystem.Application.DTOs.Requests.Bookings;

namespace WaterbusSystem.Application.Interfaces.Services;

public interface ICheckoutSeatChangeService
{
    Task<decimal> UpdateAsync(Guid orderId, Guid bookingId, UpdateCheckoutSeatsRequest request, string? accessToken, CancellationToken cancellationToken = default);
}