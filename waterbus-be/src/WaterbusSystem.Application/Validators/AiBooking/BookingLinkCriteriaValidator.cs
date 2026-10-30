using FluentValidation;
using WaterbusSystem.Application.Features.AiBooking.Models;

namespace WaterbusSystem.Application.Validators.AiBooking;

/// <summary>
/// Validator kiểm tra tính hợp lệ của tham số tạo đường dẫn đặt vé.
/// </summary>
public class BookingLinkCriteriaValidator : AbstractValidator<BookingLinkCriteria>
{
    public BookingLinkCriteriaValidator()
    {
        RuleFor(x => x.TripId)
            .NotEmpty().WithMessage("TripId không được để trống.");

        RuleFor(x => x.PassengerCount)
            .InclusiveBetween(1, 50).WithMessage("Số lượng hành khách phải từ 1 đến 50.")
            .When(x => x.PassengerCount.HasValue);
    }
}
