using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;
using ValidationException = WaterbusSystem.Application.Common.Exceptions.ValidationException;

namespace WaterbusSystem.Application.Features.Refunds.Commands.ConfirmManualRefund;

public sealed record ConfirmManualRefundResultDto(
    Guid RefundTransactionId, decimal Amount, string Status, DateTimeOffset? CompletedAt);

/// <summary>
/// BR-RF-10 - nhánh hoàn tiền THỦ CÔNG của Admin: dùng khi gateway không hỗ trợ hoàn tự động,
/// hoặc trả lỗi không thể xử lý tự động, hoặc hoàn qua kênh đối soát ngoại tuyến.
///
/// Ràng buộc bắt buộc:
///   • Chỉ Admin được gọi (policy "AdminOnly" ở tầng WebApi).
///   • Chỉ ghi Succeeded SAU KHI đã xác minh khoản hoàn thực sự chuyển đến người nhận.
///   • Bắt buộc có bằng chứng/định danh đối soát và người xác nhận - DB đã enforce bằng
///     check constraint CK_FinancialTransactions_ManualEvidence.
///   • Chống tạo lần hoàn thứ hai: không được xác nhận một Refund đã Succeeded.
/// </summary>
public record ConfirmManualRefundCommand(
    Guid RefundTransactionId,
    string Evidence,
    string? GatewayReference) : IRequest<ConfirmManualRefundResultDto>;

public class ConfirmManualRefundCommandValidator : AbstractValidator<ConfirmManualRefundCommand>
{
    public ConfirmManualRefundCommandValidator()
    {
        RuleFor(x => x.RefundTransactionId)
            .NotEmpty().WithMessage("Mã giao dịch hoàn tiền không được để trống.");

        RuleFor(x => x.Evidence)
            .NotEmpty().WithMessage("Phải cung cấp bằng chứng đã hoàn tiền (mã chuyển khoản, ảnh biên nhận...).")
            .MaximumLength(500).WithMessage("Bằng chứng không được vượt quá 500 ký tự.");
    }
}

public class ConfirmManualRefundCommandHandler
    : IRequestHandler<ConfirmManualRefundCommand, ConfirmManualRefundResultDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public ConfirmManualRefundCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<ConfirmManualRefundResultDto> Handle(
        ConfirmManualRefundCommand request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(_currentUserService.UserId, out var adminId))
        {
            throw new ValidationException(new List<FluentValidation.Results.ValidationFailure>
            {
                new(nameof(ConfirmManualRefundCommand.RefundTransactionId),
                    "Không xác định được người quản trị đang xác nhận hoàn tiền.")
            });
        }

        var refund = await _context.FinancialTransactions
            .Include(t => t.RefundAllocations)
            .FirstOrDefaultAsync(
                t => t.Id == request.RefundTransactionId
                     && t.TransactionType == FinancialTransactionType.Refund,
                cancellationToken)
            ?? throw new NotFoundException(nameof(FinancialTransaction), request.RefundTransactionId);

        if (refund.Status == FinancialTransactionStatus.Succeeded)
        {
            // BR-RF-10: chống tạo lần hoàn thứ hai khi Admin bấm nhầm hoặc retry.
            throw new ConcurrencyException("Giao dịch hoàn tiền này đã được xác nhận hoàn thành trước đó.");
        }

        if (refund.Status == FinancialTransactionStatus.Failed)
        {
            throw new ValidationException(new List<FluentValidation.Results.ValidationFailure>
            {
                new(nameof(ConfirmManualRefundCommand.RefundTransactionId),
                    "Giao dịch hoàn tiền đã thất bại, không thể xác nhận hoàn thành.")
            });
        }

        if (refund.RefundAllocations.Count == 0)
        {
            throw new ValidationException(new List<FluentValidation.Results.ValidationFailure>
            {
                new(nameof(ConfirmManualRefundCommand.RefundTransactionId),
                    "Giao dịch hoàn tiền không có phân bổ vé nào để đối soát.")
            });
        }

        // BR-RF-10: lưu bằng chứng/định danh đối soát và người xác nhận trước khi ghi Succeeded.
        refund.Status = FinancialTransactionStatus.Succeeded;
        refund.ManualProcessedByAdminId = adminId;
        refund.ManualRefundEvidence = request.Evidence.Trim();
        refund.GatewayName ??= "Manual";
        refund.GatewayReference = request.GatewayReference ?? refund.GatewayReference;
        refund.CompletedAt = DateTimeOffset.UtcNow;
        refund.FailureReason = null;

        await _context.SaveChangesAsync(cancellationToken);

        return new ConfirmManualRefundResultDto(
            refund.Id, refund.Amount, refund.Status.ToString(), refund.CompletedAt);
    }
}