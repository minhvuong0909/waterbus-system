using FluentValidation;
using WaterbusSystem.Application.DTOs.Requests.Accounts;

namespace WaterbusSystem.Application.Validators.Accounts;

public class GetMyOrdersRequestValidator : AbstractValidator<GetMyOrdersRequest>
{
    public GetMyOrdersRequestValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1).WithMessage("PageNumber phải lớn hơn hoặc bằng 1.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("PageSize phải trong khoảng 1-100.");
    }
}
