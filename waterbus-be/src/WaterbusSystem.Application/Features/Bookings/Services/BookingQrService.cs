using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;

namespace WaterbusSystem.Application.Features.Bookings.Services;

public sealed class BookingQrService(IBookingRepository bookings, ICheckoutAccessService access, IBookingQrSigner signer) : IBookingQrService
{
    public async Task<BookingQrCredential> GetAsync(Guid orderId, Guid bookingId, string? accessToken, CancellationToken ct = default)
    {
        await access.RequireAsync(orderId, accessToken, ct);
        var booking = await bookings.GetForQrAsync(orderId, bookingId, ct);
        if (booking.Status is not (BookingStatus.Confirmed or BookingStatus.Completed) || booking.Tickets.Count == 0)
            throw new ConcurrencyException("QR chỉ cấp cho Booking đã thanh toán và phát hành Ticket.");
        return signer.Sign(booking.PublicBookingId, booking.QrCredentialVersion, booking.TripId,
            booking.QrIssuedAt ?? booking.ConfirmedAt ?? booking.Tickets.Min(t => t.IssuedAt)!.Value);
    }

    public BookingQrPublicKey GetPublicKey() => signer.GetPublicKey();
}
