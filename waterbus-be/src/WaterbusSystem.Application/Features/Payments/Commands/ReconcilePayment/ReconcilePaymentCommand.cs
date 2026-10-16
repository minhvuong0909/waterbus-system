using MediatR;
using WaterbusSystem.Application.Features.Payments.Services;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Bookings.Services;
using WaterbusSystem.Application.Features.Payments.Commands.ConfirmVnPayIpn;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;

namespace WaterbusSystem.Application.Features.Payments.Commands.ReconcilePayment;

public record ReconcilePaymentCommand(Guid OrderId, Guid PaymentId, string? AccessToken, string IpAddress) : IRequest<VnPayIpnResultDto>;
public sealed class ReconcilePaymentCommandHandler(IApplicationDbContext context, CheckoutAccessService access,
    IVnPayService gateway, PaymentConfirmationService confirmation) : IRequestHandler<ReconcilePaymentCommand, VnPayIpnResultDto>
{
    public async Task<VnPayIpnResultDto> Handle(ReconcilePaymentCommand request, CancellationToken ct)
    {
        await access.RequireAsync(request.OrderId, request.AccessToken, ct);
        var attempt = await context.FinancialTransactions.AsNoTracking().FirstOrDefaultAsync(p => p.Id == request.PaymentId &&
            p.OrderId == request.OrderId && p.TransactionType == FinancialTransactionType.Payment, ct)
            ?? throw new NotFoundException(nameof(FinancialTransaction), request.PaymentId);
        var date = attempt.GatewayRequestCreatedAt ?? throw new ConcurrencyException("Không có thời điểm request gốc để đối soát.");
        var result = await gateway.QueryTransactionAsync(attempt.MerchantReference!, date.ToOffset(TimeSpan.FromHours(7)).ToString("yyyyMMddHHmmss"), request.IpAddress, ct);
        if (!result.IsSuccess || result.MerchantReference != attempt.MerchantReference || result.Amount != attempt.Amount * 100 || result.TransactionType != "01")
            return new("99", "Reconciliation pending");
        // Querydr đã kiểm chứng chữ ký/correlation trong adapter, không giả chữ ký IPN.
        return await confirmation.ConfirmVerifiedAsync(new ConfirmVnPayIpnCommand(new Dictionary<string, string>
        {
            ["vnp_TmnCode"] = "", ["vnp_TxnRef"] = attempt.MerchantReference!,
            ["vnp_Amount"] = ((long)(attempt.Amount * 100)).ToString(),
            ["vnp_TransactionNo"] = result.TransactionNo ?? "", ["vnp_TransactionStatus"] = result.TransactionStatus ?? "",
            ["vnp_ResponseCode"] = result.TransactionStatus == "00" ? "00" : "99"
        }), ct);
    }
}
