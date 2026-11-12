using WaterbusSystem.Application.DTOs.Requests.Refunds;
using WaterbusSystem.Application.DTOs.Responses.Refunds;

namespace WaterbusSystem.Application.Interfaces.Services;

public interface IManualRefundService
{
    Task<ConfirmManualRefundResultDto> ConfirmAsync(ConfirmManualRefundRequest request, CancellationToken cancellationToken = default);

    /// <summary>Xác nhận nhiều khoản; mỗi khoản đi qua <see cref="ConfirmAsync"/> (cùng kiểm tra bằng chứng, số tiền, một lần duy nhất) và lỗi nghiệp vụ của một khoản không dừng cả lô.</summary>
    Task<BatchConfirmManualRefundsResponse> ConfirmBatchAsync(BatchConfirmManualRefundsRequest request, CancellationToken cancellationToken = default);
}
