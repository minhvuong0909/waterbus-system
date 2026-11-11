using FluentValidation;
using WaterbusSystem.Application.DTOs.Requests.CheckIns;

namespace WaterbusSystem.Application.Validators.CheckIns;

public class SetCheckInWindowRequestValidator : AbstractValidator<SetCheckInWindowRequest>
{
    public SetCheckInWindowRequestValidator()
    {
        RuleFor(x => x.CheckInCloseAt)
            .GreaterThan(x => x.CheckInOpenAt)
            .WithMessage("Giờ đóng cửa sổ check-in phải sau giờ mở.");
    }
}
