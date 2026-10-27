using MediatR;
using FluentValidation;
using WaterbusSystem.Application.DTOs.Responses.Refunds;
using WaterbusSystem.Application.Interfaces.Services;

namespace WaterbusSystem.Application.Features.Refunds.Commands.ConfirmManualRefund;

public record ConfirmManualRefundCommand(
    Guid RefundTransactionId,
    string Evidence,
    string? GatewayReference, decimal ConfirmedAmount) : IRequest<ConfirmManualRefundResultDto>;
public class ConfirmManualRefundCommandValidator : AbstractValidator<ConfirmManualRefundCommand>
{
    public ConfirmManualRefundCommandValidator()
    {
        RuleFor(x => x.RefundTransactionId)
            .NotEmpty().WithMessage("Mã giao dịch hoàn tiền không được để trống.");

        RuleFor(x => x.ConfirmedAmount).GreaterThan(0);

        RuleFor(x => x.Evidence)
            .NotEmpty().WithMessage("Phải cung cấp bằng chứng đã hoàn tiền (mã chuyển khoản, ảnh biên nhận...).")
            .MaximumLength(500).WithMessage("Bằng chứng không được vượt quá 500 ký tự.");
    }
}

public sealed class ConfirmManualRefundCommandHandler(IManualRefundService service) : IRequestHandler<ConfirmManualRefundCommand, ConfirmManualRefundResultDto>
{
    public Task<ConfirmManualRefundResultDto> Handle(ConfirmManualRefundCommand request, CancellationToken ct) => service.ConfirmAsync(new(request.RefundTransactionId, request.Evidence, request.GatewayReference, request.ConfirmedAmount), ct);
}
