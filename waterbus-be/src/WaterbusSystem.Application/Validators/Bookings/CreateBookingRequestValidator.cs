using FluentValidation;
using WaterbusSystem.Application.DTOs.Requests.Bookings;

namespace WaterbusSystem.Application.Validators.Bookings;

public class CreateBookingRequestValidator : AbstractValidator<CreateBookingRequest>
{
    public CreateBookingRequestValidator()
    {
        RuleFor(x => x.TripId)
            .NotEmpty().WithMessage("Mã chuyến tàu không được để trống.");

        RuleFor(x => x.BoardingStationId)
            .NotEmpty().WithMessage("Bạn phải chọn bến lên tàu.");

        RuleFor(x => x.DisembarkingStationId)
            .NotEmpty().WithMessage("Bạn phải chọn bến xuống tàu.");

        RuleFor(x => x.SeatIds)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Bạn phải chọn ít nhất một ghế.")
            .Must(seats => seats.Count <= 10).WithMessage("Mỗi lượt đặt tối đa 10 ghế.")
            .Must(seats => seats.Distinct().Count() == seats.Count)
                .WithMessage("Danh sách ghế không được chứa ghế trùng lặp.");
        RuleFor(x => x.Passengers)
            .Must((cmd, p) => p == null || p.Count == 0 || p.Count == (cmd.SeatIds?.Count ?? 0))
                .WithMessage("Danh sách hành khách phải khớp với số ghế đã chọn.");

        RuleForEach(x => x.Passengers).ChildRules(passenger =>
        {
            passenger.RuleFor(p => p.Name).NotEmpty().MaximumLength(150);
            passenger.RuleFor(p => p.Email).MaximumLength(150).EmailAddress().When(p => !string.IsNullOrEmpty(p.Email));
            passenger.RuleFor(p => p.Phone).MaximumLength(20);
        });

        RuleFor(x => x.CustomerName)
            .NotEmpty().WithMessage("Họ và tên người mua không được để trống.")
            .MaximumLength(150).WithMessage("Họ tên không được vượt quá 150 ký tự.");

        RuleFor(x => x.CustomerEmail)
            .NotEmpty().WithMessage("Email người mua không được để trống.")
            .EmailAddress().WithMessage("Định dạng email không hợp lệ.")
            .MaximumLength(150);

        RuleFor(x => x.CustomerPhone)
            .Matches(@"^[0-9]{10,11}$").WithMessage("Số điện thoại phải từ 10 đến 11 chữ số.")
            .When(x => !string.IsNullOrWhiteSpace(x.CustomerPhone));
    }
}

