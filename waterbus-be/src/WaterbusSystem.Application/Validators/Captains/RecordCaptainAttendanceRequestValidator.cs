using FluentValidation;
using WaterbusSystem.Application.DTOs.Requests.Captains;

namespace WaterbusSystem.Application.Validators.Captains;

public sealed class RecordCaptainAttendanceRequestValidator : AbstractValidator<RecordCaptainAttendanceRequest>
{
    public RecordCaptainAttendanceRequestValidator() { RuleFor(r => r.CaptainId).NotEmpty(); RuleFor(r => r.AssignmentVersion).NotEmpty(); RuleFor(r => r.Reason).NotEmpty().MaximumLength(1000); }
}
