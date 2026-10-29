using WaterbusSystem.Application.DTOs.Requests.Refunds;
using WaterbusSystem.Application.DTOs.Responses.Refunds;

namespace WaterbusSystem.Application.Interfaces.Services;

public interface IManualRefundService
{
    Task<ConfirmManualRefundResultDto> ConfirmAsync(ConfirmManualRefundRequest request, CancellationToken cancellationToken = default);
}
