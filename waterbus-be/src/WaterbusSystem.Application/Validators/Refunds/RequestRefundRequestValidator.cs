using FluentValidation;
using WaterbusSystem.Application.DTOs.Requests.Refunds;

namespace WaterbusSystem.Application.Validators.Refunds;

public sealed class RequestRefundRequestValidator : AbstractValidator<RequestRefundRequest>
{
    public RequestRefundRequestValidator()
    {
        RuleFor(x => x.BookingId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(200);
    }
}
