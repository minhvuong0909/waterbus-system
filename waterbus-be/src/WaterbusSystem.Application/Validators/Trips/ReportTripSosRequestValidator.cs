using FluentValidation;
using WaterbusSystem.Application.DTOs.Requests.Trips;

namespace WaterbusSystem.Application.Validators.Trips;

public class ReportTripSosRequestValidator : AbstractValidator<ReportTripSosRequest>
{
    public ReportTripSosRequestValidator()
    {
        RuleFor(x => x.Note).MaximumLength(500).WithMessage("Ghi chú tối đa 500 ký tự.");
    }
}
