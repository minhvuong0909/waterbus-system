using FluentValidation;
using WaterbusSystem.Application.DTOs.Requests.Boats;

namespace WaterbusSystem.Application.Validators.Boats;

public class CreateBoatRequestValidator : AbstractValidator<CreateBoatRequest>
{
    public CreateBoatRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Mã tàu không được để trống.")
            .MaximumLength(20).WithMessage("Mã tàu tối đa 20 ký tự.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên tàu không được để trống.")
            .MaximumLength(100).WithMessage("Tên tàu tối đa 100 ký tự.");

        RuleFor(x => x.RegistrationNumber)
            .NotEmpty().WithMessage("Số đăng kiểm không được để trống.")
            .MaximumLength(50).WithMessage("Số đăng kiểm tối đa 50 ký tự.");

        RuleFor(x => x.TotalSeats)
            .GreaterThan(0).WithMessage("Tổng số ghế phải lớn hơn 0.");

        RuleFor(x => x.YearBuilt)
            .GreaterThan(1900).LessThanOrEqualTo(DateTime.UtcNow.Year + 1)
            .WithMessage("Năm đóng tàu không hợp lệ.");
    }
}
