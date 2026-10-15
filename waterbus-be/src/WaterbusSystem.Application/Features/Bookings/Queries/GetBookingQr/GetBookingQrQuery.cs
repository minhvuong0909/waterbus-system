using MediatR;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Bookings.Services;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;

namespace WaterbusSystem.Application.Features.Bookings.Queries.GetBookingQr;

public record GetBookingQrQuery(Guid OrderId, Guid BookingId, string? AccessToken) : IRequest<BookingQrCredential>;
public sealed class GetBookingQrQueryHandler(IApplicationDbContext context, CheckoutAccessService access,
    IBookingQrSigner signer) : IRequestHandler<GetBookingQrQuery, BookingQrCredential>
{
    public async Task<BookingQrCredential> Handle(GetBookingQrQuery request, CancellationToken ct)
    {
        await access.RequireAsync(request.OrderId, request.AccessToken, ct);
        var booking = await context.Bookings.AsNoTracking().Include(b => b.Tickets)
            .FirstOrDefaultAsync(b => b.Id == request.BookingId && b.OrderId == request.OrderId, ct)
            ?? throw new NotFoundException(nameof(Booking), request.BookingId);
        if (booking.Status is not (BookingStatus.Confirmed or BookingStatus.Completed) || booking.Tickets.Count == 0)
            throw new ConcurrencyException("QR chỉ cấp cho Booking đã thanh toán và phát hành Ticket.");
        return signer.Sign(booking.PublicBookingId, booking.QrCredentialVersion, booking.TripId,
            booking.QrIssuedAt ?? booking.ConfirmedAt ?? booking.Tickets.Min(t => t.IssuedAt)!.Value);
    }
}
