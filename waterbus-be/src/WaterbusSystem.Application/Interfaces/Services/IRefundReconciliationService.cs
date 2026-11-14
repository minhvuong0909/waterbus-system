using WaterbusSystem.Application.DTOs.Requests.Refunds;
using WaterbusSystem.Application.DTOs.Responses.Refunds;

namespace WaterbusSystem.Application.Interfaces.Services;

public interface IRefundReconciliationService
{
    Task<string> ReconcileAsync(Guid refundId, string ipAddress, CancellationToken ct = default);
    Task<WaterbusSystem.Application.Common.Models.PaginatedList<ReconciliationItemResponse>> ListPendingAsync(int page, int pageSize, CancellationToken ct = default);
}
