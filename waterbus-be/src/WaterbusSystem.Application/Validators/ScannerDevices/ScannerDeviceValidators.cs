using FluentValidation;
using WaterbusSystem.Application.DTOs.Requests.ScannerDevices;

namespace WaterbusSystem.Application.Validators.ScannerDevices;

public class RegisterScannerDeviceRequestValidator : AbstractValidator<RegisterScannerDeviceRequest>
{
    public RegisterScannerDeviceRequestValidator()
    {
        RuleFor(x => x.DeviceCode).NotEmpty().MaximumLength(50)
            .Matches("^[A-Za-z0-9._-]+$").WithMessage("DeviceCode chỉ gồm chữ, số, '.', '_', '-'.");
        RuleFor(x => x.Label).MaximumLength(100);
    }
}

public class UpdateScannerDeviceRequestValidator : AbstractValidator<UpdateScannerDeviceRequest>
{
    public UpdateScannerDeviceRequestValidator() => RuleFor(x => x.Label).MaximumLength(100);
}

public class GetScannerDevicesRequestValidator : AbstractValidator<GetScannerDevicesRequest>
{
    public GetScannerDevicesRequestValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Search).MaximumLength(50);
    }
}
