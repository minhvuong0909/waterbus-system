using WaterbusSystem.Application.Common.Interfaces;

namespace WaterbusSystem.Application.Interfaces.Services;

public interface IBookingQrService
{
    Task<BookingQrCredential> GetAsync(Guid orderId, Guid bookingId, string? accessToken, CancellationToken ct = default);
    BookingQrPublicKey GetPublicKey();
}
