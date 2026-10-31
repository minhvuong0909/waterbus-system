using FluentValidation;
using Microsoft.Extensions.Options;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Requests.Tracking;
using WaterbusSystem.Application.Features.Tracking;

namespace WaterbusSystem.Application.Validators.Tracking;

public class RecordBoatPositionRequestValidator : AbstractValidator<RecordBoatPositionRequest>
{
    public RecordBoatPositionRequestValidator(IDateTimeService dateTime, IOptions<TrackingOptions> options)
    {
        RuleFor(x => x.BoatId).NotEmpty().WithMessage("Thiếu mã tàu.");

        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90m, 90m).WithMessage("Vĩ độ phải nằm trong [-90, 90].");

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180m, 180m).WithMessage("Kinh độ phải nằm trong [-180, 180].");

        RuleFor(x => x.SpeedKnots)
            .InclusiveBetween(0m, 100m).When(x => x.SpeedKnots.HasValue)
            .WithMessage("Tốc độ (hải lý/giờ) phải nằm trong [0, 100].");

        RuleFor(x => x.ObservedAt)
            .Must(t => t <= dateTime.Now.AddMinutes(1))
            .WithMessage("Thời điểm ghi nhận vị trí không được ở tương lai.")
            .Must(t => t >= dateTime.Now.AddMinutes(-options.Value.MaxPositionAgeMinutes))
            .WithMessage("Vị trí quá cũ, không được ghi nhận.");

        RuleFor(x => x.Source)
            .Must(s => s is BoatPositionSources.Gps or BoatPositionSources.Simulated)
            .WithMessage("Nguồn vị trí chỉ được là GPS hoặc Simulated.");
    }
}
