using FluentValidation;
using WaterbusSystem.Application.DTOs.Requests.Refunds;

namespace WaterbusSystem.Application.Validators.Refunds;

public class ConfirmManualRefundRequestValidator : AbstractValidator<ConfirmManualRefundRequest>
{
    public ConfirmManualRefundRequestValidator()
    {
        RuleFor(x => x.RefundTransactionId)
            .NotEmpty().WithMessage("Mã giao dịch hoàn tiền không được để trống.");

        RuleFor(x => x.ConfirmedAmount).GreaterThan(0);

        RuleFor(x => x.Evidence)
            .NotEmpty().WithMessage("Phải cung cấp bằng chứng đã hoàn tiền (mã chuyển khoản, ảnh biên nhận...).")
            .MaximumLength(500).WithMessage("Bằng chứng không được vượt quá 500 ký tự.");
    }
}
