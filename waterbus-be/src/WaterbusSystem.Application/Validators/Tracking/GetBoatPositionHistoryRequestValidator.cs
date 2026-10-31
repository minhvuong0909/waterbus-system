using FluentValidation;
using WaterbusSystem.Application.DTOs.Requests.Tracking;

namespace WaterbusSystem.Application.Validators.Tracking;

public class GetBoatPositionHistoryRequestValidator : AbstractValidator<GetBoatPositionHistoryRequest>
{
    public GetBoatPositionHistoryRequestValidator()
    {
        RuleFor(x => x.To).GreaterThan(x => x.From).WithMessage("Thời điểm kết thúc phải sau thời điểm bắt đầu.");
        RuleFor(x => x)
            .Must(x => x.To - x.From <= TimeSpan.FromDays(7))
            .WithName("To")
            .WithMessage("Chỉ được truy vấn tối đa 7 ngày mỗi lần.");
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1).WithMessage("Số trang phải từ 1.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 500).WithMessage("Kích thước trang phải trong [1, 500].");
    }
}
