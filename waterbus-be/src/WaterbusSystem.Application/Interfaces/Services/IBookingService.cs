using WaterbusSystem.Application.DTOs.Requests.Bookings;
using WaterbusSystem.Application.DTOs.Responses.Bookings;

namespace WaterbusSystem.Application.Interfaces.Services;

public interface IBookingService
{
    Task<BookingResponse> CreateAsync(CreateBookingRequest request, CancellationToken ct = default);
}
