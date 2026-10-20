using FluentValidation;
using WaterbusSystem.Application.Features.AiBooking.Models;

namespace WaterbusSystem.Application.Features.AiBooking.Validators;

/// <summary>
/// Validator kiểm tra tính hợp lệ của tham số tìm chuyến được trích xuất từ câu hỏi của khách.
/// </summary>
public class TripSearchCriteriaValidator : AbstractValidator<TripSearchCriteria>
{
    public TripSearchCriteriaValidator()
    {
        RuleFor(x => x.PassengerCount)
            .InclusiveBetween(1, 50).WithMessage("Số lượng hành khách phải từ 1 đến 50.")
            .When(x => x.PassengerCount.HasValue);

        RuleFor(x => x.EarliestDepartureTime)
            .LessThanOrEqualTo(x => x.LatestDepartureTime)
            .When(x => x.EarliestDepartureTime.HasValue && x.LatestDepartureTime.HasValue)
            .WithMessage("Giờ bắt đầu phải trước hoặc bằng giờ kết thúc.");

        RuleFor(x => x.DepartureDate)
            .Must(d => !d.HasValue || d.Value >= DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).DateTime))
            .WithMessage("Ngày khởi hành không được là ngày trong quá khứ.")
            .When(x => x.DepartureDate.HasValue);

        When(x => !string.IsNullOrEmpty(x.TripType), () =>
        {
            RuleFor(x => x.TripType)
                .Must(t => t == "Commuter" || t == "Sightseeing")
                .WithMessage("Loại chuyến tìm kiếm chỉ được là 'Commuter' hoặc 'Sightseeing'.");
        });
    }
}
