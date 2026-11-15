using FluentValidation;
using WaterbusSystem.Application.DTOs.Requests.Stations;

namespace WaterbusSystem.Application.Validators.Stations;

public class UpdateStationRequestValidator : AbstractValidator<UpdateStationRequest>
{
    public UpdateStationRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên bến không được để trống.")
            .MaximumLength(150).WithMessage("Tên bến tối đa 150 ký tự.");

        RuleFor(x => x.Address)
            .MaximumLength(300).WithMessage("Địa chỉ tối đa 300 ký tự.");

        // Chỉ chặn toạ độ không tồn tại trên Trái Đất. Chưa giới hạn theo địa bàn TP.HCM/Đồng Nai vì BR chưa chốt.
        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90, 90).WithMessage("Vĩ độ phải nằm trong khoảng từ -90 đến 90.");

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180, 180).WithMessage("Kinh độ phải nằm trong khoảng từ -180 đến 180.");

        RuleFor(x => x.OrderIndex)
            .GreaterThanOrEqualTo(0).WithMessage("Thứ tự bến phải lớn hơn hoặc bằng 0.");
    }
}
