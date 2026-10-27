using WaterbusSystem.Application.DTOs.Requests.Refunds;
using WaterbusSystem.Application.DTOs.Responses.Refunds;

namespace WaterbusSystem.Application.Interfaces.Services;

public interface IRefundRequestService
{
    Task<RequestRefundResultDto> RequestAsync(RequestRefundRequest request, CancellationToken cancellationToken = default);
}
