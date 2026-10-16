using MediatR;
using WaterbusSystem.Application.Common.Interfaces;

namespace WaterbusSystem.Application.Features.Bookings.Queries.GetBookingQrPublicKey;

public record GetBookingQrPublicKeyQuery : IRequest<BookingQrPublicKey>;
public sealed class GetBookingQrPublicKeyQueryHandler(IBookingQrSigner signer) : IRequestHandler<GetBookingQrPublicKeyQuery, BookingQrPublicKey>
{
    public Task<BookingQrPublicKey> Handle(GetBookingQrPublicKeyQuery request, CancellationToken ct) => Task.FromResult(signer.GetPublicKey());
}
