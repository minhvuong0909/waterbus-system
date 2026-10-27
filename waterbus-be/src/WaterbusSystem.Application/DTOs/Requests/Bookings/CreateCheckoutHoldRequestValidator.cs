using FluentValidation;

namespace WaterbusSystem.Application.DTOs.Requests.Bookings;

public sealed class CreateCheckoutHoldRequestValidator : AbstractValidator<CreateCheckoutHoldRequest>
{
    public CreateCheckoutHoldRequestValidator()
    {
        RuleFor(x => x.Legs)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .Must(legs => legs is { Count: 1 or 2 })
            .WithMessage("Checkout phải có 1 hoặc 2 lượt.");

        RuleForEach(x => x.Legs)
            .NotNull()
            .WithMessage("Thông tin lượt đi không hợp lệ.");

        RuleForEach(x => x.Legs)
            .SetValidator(new CheckoutHoldLegRequestValidator());
    }
}

public sealed class CheckoutHoldLegRequestValidator : AbstractValidator<CheckoutHoldLegRequest>
{
    public CheckoutHoldLegRequestValidator()
    {
        RuleFor(x => x.TripId)
            .NotEmpty();

        RuleFor(x => x.BoardingStationId)
            .NotEmpty();

        RuleFor(x => x.DisembarkingStationId)
            .NotEmpty();

        RuleFor(x => x.SeatIds)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Phải chọn ít nhất một ghế.")
            .Must(seats => seats.Count <= 10)
            .WithMessage("Mỗi lượt được chọn tối đa 10 ghế.")
            .Must(seats => seats.Distinct().Count() == seats.Count)
            .WithMessage("Danh sách ghế không được trùng lặp.");
    }
}
