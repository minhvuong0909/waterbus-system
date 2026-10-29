using WaterbusSystem.Application.DTOs.Responses.Payments;

namespace WaterbusSystem.Application.Interfaces.Services;

public interface IPaymentService
{
    Task<CreateVnPayUrlResponseDto> CreateVnPayUrlAsync(Guid orderId, string ipAddress, string? accessToken, CancellationToken ct = default);
    Task<VnPayIpnResultDto> ConfirmIpnAsync(IReadOnlyDictionary<string, string> query, CancellationToken ct = default);
    Task<VnPayReturnDto> GetReturnAsync(IReadOnlyDictionary<string, string> query, CancellationToken ct = default);
    Task<VnPayIpnResultDto> ReconcileAsync(Guid orderId, Guid paymentId, string? accessToken, string ipAddress, CancellationToken ct = default);
}
