using MediatR;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Scanner.Queries.GetScannerManifest;

namespace WaterbusSystem.Application.Features.Scanner.Queries.GetScannerPublicKey;

/// <summary>Lấy khóa công khai RS256 đang dùng để xác minh QR do hệ thống phát hành.</summary>
public sealed record GetScannerPublicKeyQuery : IRequest<ScannerPublicKeyDto>;

public sealed class GetScannerPublicKeyQueryHandler(IBookingQrSigner signer)
    : IRequestHandler<GetScannerPublicKeyQuery, ScannerPublicKeyDto>
{
    public Task<ScannerPublicKeyDto> Handle(
        GetScannerPublicKeyQuery request,
        CancellationToken cancellationToken)
    {
        var key = signer.GetPublicKey();
        return Task.FromResult(new ScannerPublicKeyDto(key.Kid, key.Algorithm, key.PublicKeyPem, true));
    }
}
