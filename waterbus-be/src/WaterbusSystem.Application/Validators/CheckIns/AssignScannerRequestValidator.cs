using FluentValidation;
using WaterbusSystem.Application.DTOs.Requests.CheckIns;

namespace WaterbusSystem.Application.Validators.CheckIns;

public class AssignScannerRequestValidator : AbstractValidator<AssignScannerRequest>
{
    public AssignScannerRequestValidator()
    {
        RuleFor(x => x.DeviceId).NotEmpty().WithMessage("Thiếu mã thiết bị.");
        RuleFor(x => x.StaffAccountId).NotEmpty().WithMessage("Thiếu mã nhân viên.");
        RuleFor(x => x.TripStopCallId).NotEmpty().WithMessage("Thiếu mã điểm dừng.");
        RuleFor(x => x.ActiveUntil)
            .GreaterThan(x => x.ActiveFrom)
            .When(x => x.ActiveUntil.HasValue)
            .WithMessage("Thời điểm kết thúc phân công phải sau thời điểm bắt đầu.");
    }
}
