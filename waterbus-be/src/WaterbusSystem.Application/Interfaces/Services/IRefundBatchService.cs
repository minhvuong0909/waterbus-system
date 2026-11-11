using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.Refunds;
using WaterbusSystem.Application.DTOs.Responses.Refunds;

namespace WaterbusSystem.Application.Interfaces.Services;

public interface IRefundBatchService
{
    /// <summary>Chỉ đọc: Booking của chuyến Cancelled/Terminated có vé đủ điều kiện và chưa được hoàn.</summary>
    Task<PaginatedList<RefundCandidateResponse>> ListCandidatesAsync(ListRefundCandidatesRequest request, CancellationToken ct = default);

    /// <summary>Tạo refund (qua IRefundRequestService, cùng quy tắc và chống lặp) cho mọi candidate của chuyến.</summary>
    Task<BatchRefundRequestResultResponse> RequestForTripAsync(BatchRequestRefundsRequest request, CancellationToken ct = default);
}
