using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.ScannerDevices;
using WaterbusSystem.Application.DTOs.Responses.ScannerDevices;

namespace WaterbusSystem.Application.Interfaces.Services;

public interface IStationScannerService
{
    Task<Guid> CreateAsync(CreateStationScannerRequest request, CancellationToken ct);
    Task<ScannerInvitationResponse> InviteAsync(Guid id, CancellationToken ct);
    Task CancelInvitationAsync(Guid id, CancellationToken ct);
    Task SuspendAsync(Guid id, CancellationToken ct);
    Task RevokeAsync(Guid id, CancellationToken ct);
    Task ResetAsync(Guid id, CancellationToken ct);
    Task TransferAsync(Guid id, TransferScannerRequest request, CancellationToken ct);
    Task<ScannerPairingResponse> PairAsync(RedeemScannerInvitationRequest request, CancellationToken ct);
    Task ResolveReviewAsync(Guid deviceId, Guid eventId, ResolveScannerReviewRequest request, CancellationToken ct);
    Task<PaginatedList<ScannerAuditResponse>> ListAuditAsync(Guid id, int page, int size, CancellationToken ct);
}
