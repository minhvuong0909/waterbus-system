using FluentValidation;
using WaterbusSystem.Application.DTOs.Requests.CheckIns;

namespace WaterbusSystem.Application.Validators.CheckIns;

public class SubmitCheckInRequestValidator : AbstractValidator<SubmitCheckInRequest>
{
    public SubmitCheckInRequestValidator()
    {
        RuleFor(x => x.TicketId).NotEmpty().WithMessage("Thiếu mã vé.");
        RuleFor(x => x.TripStopCallId).NotEmpty().WithMessage("Thiếu mã điểm dừng.");
        RuleFor(x => x.ClientEventId)
            .NotEmpty().WithMessage("Thiếu định danh lần quét.")
            .MaximumLength(100).WithMessage("Định danh lần quét tối đa 100 ký tự.");
        RuleFor(x => x.Note).MaximumLength(500).WithMessage("Ghi chú tối đa 500 ký tự.");
    }
}
