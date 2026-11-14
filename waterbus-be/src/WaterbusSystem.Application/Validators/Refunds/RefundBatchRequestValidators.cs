using FluentValidation;
using WaterbusSystem.Application.DTOs.Requests.Refunds;

namespace WaterbusSystem.Application.Validators.Refunds;

public sealed class ListRefundCandidatesRequestValidator : AbstractValidator<ListRefundCandidatesRequest>
{
    public ListRefundCandidatesRequestValidator()
    {
        RuleFor(x => x.TripId).NotEmpty();
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class BatchRequestRefundsRequestValidator : AbstractValidator<BatchRequestRefundsRequest>
{
    public BatchRequestRefundsRequestValidator()
    {
        RuleFor(x => x.TripId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(200);
    }
}
