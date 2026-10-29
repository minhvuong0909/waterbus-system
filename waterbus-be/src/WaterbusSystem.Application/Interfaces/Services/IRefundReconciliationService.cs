using WaterbusSystem.Application.DTOs.Requests.Refunds;
using WaterbusSystem.Application.DTOs.Responses.Refunds;

namespace WaterbusSystem.Application.Interfaces.Services;

public interface IRefundReconciliationService
{
    Task<string> ReconcileAsync(Guid refundId, string ipAddress, CancellationToken ct = default);
}
