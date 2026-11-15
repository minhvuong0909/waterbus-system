using FluentValidation;
using WaterbusSystem.Application.DTOs.Requests.Captains;

namespace WaterbusSystem.Application.Validators.Captains;

public sealed class CreateCaptainRequestValidator : AbstractValidator<CreateCaptainRequest>
{
    public CreateCaptainRequestValidator()
    {
        RuleFor(r => r.FullName).NotEmpty().MaximumLength(200);
        RuleFor(r => r.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(r => r.Phone).NotEmpty().MaximumLength(30).Matches(@"^\+?[0-9 ()-]{8,30}$");
        RuleFor(r => r.LicenseNumber).NotEmpty().MaximumLength(100);
        RuleFor(r => r.LicenseExpiresOn).NotEmpty();
    }
}
