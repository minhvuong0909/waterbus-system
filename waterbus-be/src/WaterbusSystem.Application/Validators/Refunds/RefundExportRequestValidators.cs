using FluentValidation;
using WaterbusSystem.Application.DTOs.Requests.Refunds;

namespace WaterbusSystem.Application.Validators.Refunds;

public sealed class ExportRefundsRequestValidator : AbstractValidator<ExportRefundsRequest>
{
    public ExportRefundsRequestValidator()
    {
        RuleFor(x => x.TripId).NotEqual(Guid.Empty).When(x => x.TripId.HasValue);
    }
}

public sealed class BatchConfirmManualRefundsRequestValidator : AbstractValidator<BatchConfirmManualRefundsRequest>
{
    public const int MaxItems = 100;

    public BatchConfirmManualRefundsRequestValidator()
    {
        RuleFor(x => x.Items).NotNull().Must(i => i is { Count: > 0 and <= MaxItems })
            .WithMessage($"Cần từ 1 đến {MaxItems} khoản hoàn mỗi lần xác nhận.");
        RuleFor(x => x.Items)
            .Must(i => i.Select(x => x.RefundTransactionId).Distinct().Count() == i.Count)
            .When(x => x.Items is not null)
            .WithMessage("Mỗi khoản hoàn chỉ được xuất hiện một lần trong lô.");
        RuleForEach(x => x.Items).SetValidator(new ConfirmManualRefundRequestValidator());
    }
}
