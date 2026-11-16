using FluentValidation;
using WaterbusSystem.Application.DTOs.Requests.StaffAccounts;

namespace WaterbusSystem.Application.Validators.StaffAccounts;

internal static class StaffAccountRules
{
    public const string PhonePattern = @"^\+?[0-9 ()-]{8,30}$";
}

public sealed class CreateStaffAccountRequestValidator : AbstractValidator<CreateStaffAccountRequest>
{
    public CreateStaffAccountRequestValidator()
    {
        RuleFor(r => r.FullName).NotEmpty().MaximumLength(200);
        RuleFor(r => r.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(r => r.Phone).NotEmpty().MaximumLength(30).Matches(StaffAccountRules.PhonePattern);
        RuleFor(r => r.StaffCode).MaximumLength(50);
    }
}

public sealed class UpdateStaffAccountRequestValidator : AbstractValidator<UpdateStaffAccountRequest>
{
    public UpdateStaffAccountRequestValidator()
    {
        RuleFor(r => r.FullName).NotEmpty().MaximumLength(200);
        RuleFor(r => r.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(r => r.Phone).NotEmpty().MaximumLength(30).Matches(StaffAccountRules.PhonePattern);
        RuleFor(r => r.StaffCode).MaximumLength(50);
    }
}

public sealed class ResendStaffInvitationRequestValidator : AbstractValidator<ResendStaffInvitationRequest>
{
    public ResendStaffInvitationRequestValidator() => RuleFor(r => r.Reason).NotEmpty().MaximumLength(1000);
}

public sealed class GetStaffAccountsRequestValidator : AbstractValidator<GetStaffAccountsRequest>
{
    public GetStaffAccountsRequestValidator()
    {
        RuleFor(r => r.Page).GreaterThan(0);
        RuleFor(r => r.PageSize).InclusiveBetween(1, 100);
        RuleFor(r => r.Search).MaximumLength(200);
    }
}
