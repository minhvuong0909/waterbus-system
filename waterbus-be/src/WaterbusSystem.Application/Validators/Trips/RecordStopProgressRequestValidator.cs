using FluentValidation;
using WaterbusSystem.Application.DTOs.Requests.Trips;

namespace WaterbusSystem.Application.Validators.Trips;

public class RecordStopProgressRequestValidator : AbstractValidator<RecordStopProgressRequest>
{
    public RecordStopProgressRequestValidator()
    {
        RuleFor(x => x.Action).IsInEnum().WithMessage("Thao tác không hợp lệ.");
    }
}
