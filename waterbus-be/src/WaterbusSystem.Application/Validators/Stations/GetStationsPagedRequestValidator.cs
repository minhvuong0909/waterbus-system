using FluentValidation;
using WaterbusSystem.Application.DTOs.Requests.Stations;

namespace WaterbusSystem.Application.Validators.Stations;

/// <summary>M14: chặn PageNumber/PageSize âm, bằng 0 hoặc quá lớn trước khi vào Skip/Take.</summary>
public class GetStationsPagedRequestValidator : AbstractValidator<GetStationsPagedRequest>
{
    public GetStationsPagedRequestValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1).WithMessage("PageNumber phải lớn hơn hoặc bằng 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("PageSize phải trong khoảng 1-100.");
    }
}
