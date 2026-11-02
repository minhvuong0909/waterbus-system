using FluentValidation;
using WaterbusSystem.Application.DTOs.Requests.CheckIns;

namespace WaterbusSystem.Application.Validators.CheckIns;

public class SyncCheckInEventsRequestValidator : AbstractValidator<SyncCheckInEventsRequest>
{
    private const int MaxBatchSize = 200;

    public SyncCheckInEventsRequestValidator()
    {
        RuleFor(x => x.Events)
            .NotEmpty().WithMessage("Danh sách sự kiện check-in rỗng.")
            .Must(e => e.Count <= MaxBatchSize).WithMessage($"Mỗi lô đồng bộ tối đa {MaxBatchSize} sự kiện.");

        RuleForEach(x => x.Events).ChildRules(ev =>
        {
            ev.RuleFor(e => e.TicketId).NotEmpty().WithMessage("Thiếu mã vé.");
            ev.RuleFor(e => e.TripStopCallId).NotEmpty().WithMessage("Thiếu mã điểm dừng.");
            ev.RuleFor(e => e.ClientEventId)
                .NotEmpty().WithMessage("Thiếu định danh lần quét.")
                .MaximumLength(100).WithMessage("Định danh lần quét tối đa 100 ký tự.");
            ev.RuleFor(e => e.Note).MaximumLength(500).WithMessage("Ghi chú tối đa 500 ký tự.");
        });
    }
}
