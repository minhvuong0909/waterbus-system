using FluentValidation;
using WaterbusSystem.Application.DTOs.Requests.Incidents;

namespace WaterbusSystem.Application.Validators.Incidents;

public class GetIncidentsRequestValidator : AbstractValidator<GetIncidentsRequest>
{
    public GetIncidentsRequestValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1).WithMessage("Số trang phải từ 1.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("Kích thước trang phải trong [1, 100].");
    }
}