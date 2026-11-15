using FluentValidation;
using WaterbusSystem.Application.DTOs.Requests.SightseeingAudio;

namespace WaterbusSystem.Application.Validators.SightseeingAudio;

/// <summary>
/// Kiểm tra tính hợp lệ của tham số cập nhật thời gian chuyến và ngưỡng báo bến.
/// Chỉ kiểm tra các ràng buộc tĩnh (tương đối giữa các trường và giới hạn tuyệt đối).
/// Ràng buộc phụ thuộc số chặng thực tế của tuyến được kiểm tra ở StopAnnouncementAdminService
/// vì cần truy vấn CSDL để dựng ngân sách thời gian.
/// </summary>
public sealed class UpdateStopAnnouncementRequestValidator : AbstractValidator<UpdateStopAnnouncementRequest>
{
    public UpdateStopAnnouncementRequestValidator()
    {
        RuleFor(x => x.TripDurationSeconds)
            .InclusiveBetween(60, 7200).When(x => x.TripDurationSeconds.HasValue)
            .WithMessage("Thời lượng toàn chuyến phải từ 60 giây đến 7200 giây.");

        RuleFor(x => x.DwellTimeSeconds)
            .InclusiveBetween(0, 120).When(x => x.DwellTimeSeconds.HasValue)
            .WithMessage("Thời gian dừng tại bến phải từ 0 đến 120 giây.");

        RuleFor(x => x.NearThreshold)
            .InclusiveBetween(0.1, 0.99).When(x => x.NearThreshold.HasValue)
            .WithMessage("Ngưỡng báo gần bến phải nằm trong khoảng từ 0.1 đến 0.99 (10% - 99%).");

        RuleFor(x => x)
            .Must(HaveDwellWithinTripDuration)
            .WithName("TripDurationSeconds")
            .WithMessage("Thời lượng toàn chuyến phải lớn hơn tổng thời gian dừng tại các bến.");
    }

    private static bool HaveDwellWithinTripDuration(UpdateStopAnnouncementRequest request)
    {
        if (!request.TripDurationSeconds.HasValue || !request.DwellTimeSeconds.HasValue)
            return true;

        return request.TripDurationSeconds.Value > request.DwellTimeSeconds.Value;
    }
}
