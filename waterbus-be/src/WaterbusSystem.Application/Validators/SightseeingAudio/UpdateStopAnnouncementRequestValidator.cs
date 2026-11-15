using FluentValidation;
using WaterbusSystem.Application.DTOs.Requests.SightseeingAudio;

namespace WaterbusSystem.Application.Validators.SightseeingAudio;

/// <summary>
/// Kiểm tra tính hợp lệ của tham số cập nhật bộ mô phỏng.
/// </summary>
public sealed class UpdateStopAnnouncementRequestValidator : AbstractValidator<UpdateStopAnnouncementRequest>
{
    public UpdateStopAnnouncementRequestValidator()
    {
        RuleFor(x => x.SegmentDurationSeconds)
            .GreaterThan(0).When(x => x.SegmentDurationSeconds.HasValue)
            .WithMessage("Thời gian mỗi chặng mô phỏng phải lớn hơn 0 giây.");

        RuleFor(x => x.DwellTimeSeconds)
            .GreaterThanOrEqualTo(0).When(x => x.DwellTimeSeconds.HasValue)
            .WithMessage("Thời gian dừng tại bến phải lớn hơn hoặc bằng 0 giây.");

        RuleFor(x => x.NearThreshold)
            .InclusiveBetween(0.1, 0.99).When(x => x.NearThreshold.HasValue)
            .WithMessage("Ngưỡng báo gần bến phải nằm trong khoảng từ 0.1 đến 0.99 (10% - 99%).");
    }
}
