using FluentValidation;
using WaterbusSystem.Application.DTOs.Requests.Stations;

namespace WaterbusSystem.Application.Validators.Stations;

public class CreateStationRequestValidator : AbstractValidator<CreateStationRequest>
{
    public CreateStationRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Mã bến không được để trống.")
            .MaximumLength(20).WithMessage("Mã bến tối đa 20 ký tự.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên bến không được để trống.")
            .MaximumLength(150).WithMessage("Tên bến tối đa 150 ký tự.");

        RuleFor(x => x.Address)
            .MaximumLength(300).WithMessage("Địa chỉ tối đa 300 ký tự.");

        RuleFor(x => x.OrderIndex)
            .GreaterThanOrEqualTo(0).WithMessage("Thứ tự bến phải lớn hơn hoặc bằng 0.");
    }
}
