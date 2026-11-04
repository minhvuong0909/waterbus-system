using FluentValidation;
using WaterbusSystem.Application.DTOs.Requests.Trips;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Rules;

namespace WaterbusSystem.Application.Validators.Trips;

public class ChangeTripStatusRequestValidator : AbstractValidator<ChangeTripStatusRequest>
{
    public ChangeTripStatusRequestValidator()
    {
        RuleFor(x => x.Operation).IsInEnum().WithMessage("Thao tác không hợp lệ.");

        RuleFor(x => x.Reason)
            .NotEmpty().When(x => TripLifecycle.RequiresReason(x.Operation))
            .WithMessage("Cần nhập lý do cho thao tác này.");
        RuleFor(x => x.Reason).MaximumLength(500).WithMessage("Lý do tối đa 500 ký tự.");

        RuleFor(x => x.IncidentId)
            .NotNull().When(x => x.Operation == TripOperation.NoAction)
            .WithMessage("Quyết định \"không tác động\" phải gắn với một sự cố.");
    }
}
