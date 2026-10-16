using MediatR;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;

namespace WaterbusSystem.Application.Features.Refunds.Commands.ReconcileRefund;

public record ReconcileRefundCommand(Guid RefundId, string IpAddress) : IRequest<string>;
public sealed class ReconcileRefundCommandHandler(IApplicationDbContext context, IVnPayService gateway,
    ICommercialTransactionRunner transactions) : IRequestHandler<ReconcileRefundCommand, string>
{
    public async Task<string> Handle(ReconcileRefundCommand request, CancellationToken ct)
    {
        var snapshot = await context.FinancialTransactions.AsNoTracking().Include(r => r.OriginalPayment)
            .FirstOrDefaultAsync(r => r.Id == request.RefundId && r.TransactionType == FinancialTransactionType.Refund, ct)
            ?? throw new NotFoundException(nameof(FinancialTransaction), request.RefundId);
        if (snapshot.Status != FinancialTransactionStatus.Processing || snapshot.GatewayName != "VNPAY") return snapshot.Status.ToString();
        var payment = snapshot.OriginalPayment!;
        var date = payment.GatewayRequestCreatedAt ?? throw new ConcurrencyException("Thiếu request gốc để đối soát.");
        var response = await gateway.QueryTransactionAsync(payment.MerchantReference!, date.ToOffset(TimeSpan.FromHours(7)).ToString("yyyyMMddHHmmss"), request.IpAddress, ct);
        return await transactions.RunAsync(async () =>
        {
            var refund = await context.FinancialTransactions.FirstAsync(r => r.Id == request.RefundId, ct);
            if (refund.Status != FinancialTransactionStatus.Processing) return refund.Status.ToString();
            // Query của payment không đủ để xác nhận một partial refund: cần khớp định danh + số tiền khoản hoàn.
            var matches = response.IsSuccess && response.MerchantReference == payment.MerchantReference &&
                response.TransactionType is "02" or "03" && !string.IsNullOrEmpty(refund.GatewayReference) &&
                response.TransactionNo == refund.GatewayReference && response.Amount == refund.Amount * 100;
            if (matches && response.TransactionStatus == "00")
            {
                refund.Status = FinancialTransactionStatus.Succeeded; refund.CompletedAt = DateTimeOffset.UtcNow;
                refund.FailureReason = null;
            }
            else if (matches && response.TransactionStatus == "09")
            {
                refund.Status = FinancialTransactionStatus.Requested;
                refund.FailureReason = "Gateway definitively rejected refund; manual action required";
            }
            await context.SaveChangesAsync(ct);
            return refund.Status.ToString();
        }, ct);
    }
}
