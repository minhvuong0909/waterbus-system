using FluentValidation;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Requests.Refunds;
using WaterbusSystem.Application.DTOs.Responses.Refunds;
using WaterbusSystem.Application.Interfaces.Persistence;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;
using ValidationException = WaterbusSystem.Application.Common.Exceptions.ValidationException;

namespace WaterbusSystem.Application.Features.Refunds.Services;

public sealed class RefundReconciliationService(IFinancialTransactionRepository refunds, IUnitOfWork unitOfWork,
    IVnPayService gateway, ICommercialTransactionRunner transactions) : IRefundReconciliationService
{
    public async Task<string> ReconcileAsync(Guid refundId, string ipAddress, CancellationToken ct = default)
    {
        var snapshot = await refunds.GetRefundSnapshotAsync(refundId, ct);
        if (snapshot.Status != FinancialTransactionStatus.Processing || snapshot.GatewayName != "VNPAY") return snapshot.Status.ToString();
        var payment = snapshot.OriginalPayment!;
        var date = payment.GatewayRequestCreatedAt ?? throw new ConcurrencyException("Thiếu request gốc để đối soát.");
        var response = await gateway.QueryTransactionAsync(payment.MerchantReference!, date.ToOffset(TimeSpan.FromHours(7)).ToString("yyyyMMddHHmmss"), ipAddress, ct);
        return await transactions.RunAsync(async () =>
        {
            var refund = await refunds.GetRefundForUpdateAsync(refundId, ct);
            if (refund.Status != FinancialTransactionStatus.Processing) return refund.Status.ToString();
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
            await unitOfWork.SaveChangesAsync(ct);
            return refund.Status.ToString();
        }, ct);
    }
}
