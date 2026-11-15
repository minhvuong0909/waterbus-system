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

        // Chỉ chặn tọa độ không tồn tại trên Trái Đất; chưa giới hạn theo địa bàn vì BR chưa chốt.
        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90d, 90d).WithMessage("Vĩ độ phải nằm trong khoảng [-90, 90].");

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180d, 180d).WithMessage("Kinh độ phải nằm trong khoảng [-180, 180].");

        RuleFor(x => x.OrderIndex)
            .GreaterThanOrEqualTo(0).WithMessage("Thứ tự bến phải lớn hơn hoặc bằng 0.");
    }
}
