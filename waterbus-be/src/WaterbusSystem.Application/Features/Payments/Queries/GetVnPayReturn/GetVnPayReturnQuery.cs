using MediatR;
using WaterbusSystem.Application.Common.Interfaces;

namespace WaterbusSystem.Application.Features.Payments.Queries.GetVnPayReturn;

public record VnPayReturnDto(bool Success, string MerchantReference, string ResponseCode, string Message)
{
    public string BookingCode => MerchantReference; // Alias tương thích response cũ; mã này là payment reference.
}
public record GetVnPayReturnQuery(IReadOnlyDictionary<string, string> Query) : IRequest<VnPayReturnDto>;
public sealed class GetVnPayReturnQueryHandler(IVnPayService gateway) : IRequestHandler<GetVnPayReturnQuery, VnPayReturnDto>
{
    public Task<VnPayReturnDto> Handle(GetVnPayReturnQuery request, CancellationToken ct)
    {
        var valid = gateway.ValidateSignature(request.Query) && gateway.IsValidTmnCode(request.Query.GetValueOrDefault("vnp_TmnCode", ""));
        var code = request.Query.GetValueOrDefault("vnp_ResponseCode", "");
        var success = valid && code == "00" && request.Query.GetValueOrDefault("vnp_TransactionStatus") == "00";
        return Task.FromResult(new VnPayReturnDto(success, request.Query.GetValueOrDefault("vnp_TxnRef", ""), code,
            valid ? "Kết quả chuyển hướng; xem trạng thái Order được backend xác minh." : "Chữ ký chuyển hướng không hợp lệ."));
    }
}
