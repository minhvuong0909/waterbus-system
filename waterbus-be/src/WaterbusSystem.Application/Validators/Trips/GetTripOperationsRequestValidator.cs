using FluentValidation;
using WaterbusSystem.Application.DTOs.Requests.Trips;

namespace WaterbusSystem.Application.Validators.Trips;

public class GetTripOperationsRequestValidator : AbstractValidator<GetTripOperationsRequest>
{
    public const int MaxRangeDays = 31;

    public GetTripOperationsRequestValidator()
    {
        RuleFor(x => x)
            .Must(x => x.From is null || x.To is null || x.To > x.From)
            .WithName("To").WithMessage("Thời điểm kết thúc phải sau thời điểm bắt đầu.");
        RuleFor(x => x)
            .Must(x => x.From is null || x.To is null || x.To - x.From <= TimeSpan.FromDays(MaxRangeDays))
            .WithName("To").WithMessage($"Chỉ được xem tối đa {MaxRangeDays} ngày mỗi lần.");
    }
}
