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

public sealed class ManualRefundService : IManualRefundService
{
    private readonly IFinancialTransactionRepository _refunds;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<ConfirmManualRefundRequest> _validator;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICommercialTransactionRunner _transactions;

    public ManualRefundService(IFinancialTransactionRepository refunds, IUnitOfWork unitOfWork, IValidator<ConfirmManualRefundRequest> validator, ICurrentUserService currentUserService, ICommercialTransactionRunner transactions)
    {
        _refunds = refunds; _unitOfWork = unitOfWork; _validator = validator;
        _currentUserService = currentUserService;
        _transactions = transactions;
    }

    public async Task<ConfirmManualRefundResultDto> ConfirmAsync(
        ConfirmManualRefundRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) throw new ValidationException(validation.Errors);
        return await _transactions.RunAsync(() => ConfirmWithinTransactionAsync(request, cancellationToken), cancellationToken);
    }

    private async Task<ConfirmManualRefundResultDto> ConfirmWithinTransactionAsync(ConfirmManualRefundRequest request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(_currentUserService.UserId, out var adminId))
        {
            throw new ValidationException(new List<FluentValidation.Results.ValidationFailure>
            {
                new(nameof(ConfirmManualRefundRequest.RefundTransactionId),
                    "Không xác định được người quản trị đang xác nhận hoàn tiền.")
            });
        }

        var refund = await _refunds.GetRefundForManualConfirmationAsync(request.RefundTransactionId, cancellationToken);

        if (refund.Status == FinancialTransactionStatus.Succeeded)
        {
            throw new ConcurrencyException("Giao dịch hoàn tiền này đã được xác nhận hoàn thành trước đó.");
        }

        if (refund.Status == FinancialTransactionStatus.Processing && refund.GatewayName != "Manual")
            throw new ConcurrencyException("Gateway có thể đang hoàn tiền. Phải đối soát trước khi chuyển khoản thủ công.");

        if (refund.Status == FinancialTransactionStatus.Failed)
        {
            throw new ValidationException(new List<FluentValidation.Results.ValidationFailure>
            {
                new(nameof(ConfirmManualRefundRequest.RefundTransactionId),
                    "Giao dịch hoàn tiền đã thất bại, không thể xác nhận hoàn thành.")
            });
        }

        if (refund.RefundAllocations.Count == 0 && !(refund.IdempotencyKey.StartsWith("LATE_REFUND_") &&
            refund.OriginalPayment?.Status == FinancialTransactionStatus.Succeeded &&
            refund.OriginalPayment.FailureReason?.StartsWith("ReconciliationRequired:") == true &&
            refund.Amount == refund.OriginalPayment.Amount && refund.RefundBooking?.Tickets.Count == 0))
        {
            throw new ValidationException(new List<FluentValidation.Results.ValidationFailure>
            {
                new(nameof(ConfirmManualRefundRequest.RefundTransactionId),
                    "Giao dịch hoàn tiền không có phân bổ vé nào để đối soát.")
            });
        }

        if (request.ConfirmedAmount != refund.Amount) throw new ConcurrencyException("Số tiền đối soát không khớp khoản hoàn.");
        refund.Status = FinancialTransactionStatus.Succeeded;
        refund.ManualProcessedByAdminId = adminId;
        refund.ManualRefundEvidence = request.Evidence.Trim();
        refund.GatewayName = "Manual";
        refund.GatewayReference = request.GatewayReference ?? refund.GatewayReference;
        refund.CompletedAt = DateTimeOffset.UtcNow;
        refund.FailureReason = null;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ConfirmManualRefundResultDto(
            refund.Id, refund.Amount, refund.Status.ToString(), refund.CompletedAt);
    }
}
