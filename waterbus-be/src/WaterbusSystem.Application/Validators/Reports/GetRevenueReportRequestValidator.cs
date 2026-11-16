using FluentValidation;
using WaterbusSystem.Application.DTOs.Requests.Reports;

namespace WaterbusSystem.Application.Validators.Reports;

public class GetRevenueReportRequestValidator : AbstractValidator<GetRevenueReportRequest>
{
    /// <summary>Trần khoảng thời gian một lần xem, để truy vấn gộp luôn nhỏ (tối đa 366 dòng theo ngày).</summary>
    public const int MaxRangeDays = 366;

    public GetRevenueReportRequestValidator()
    {
        RuleFor(x => x.Granularity).IsInEnum().WithMessage("Granularity phải là Day hoặc Month.");
        RuleFor(x => x.To)
            .GreaterThanOrEqualTo(x => x.From).WithMessage("Ngày kết thúc phải từ ngày bắt đầu trở đi.");
        RuleFor(x => x)
            .Must(x => x.To.DayNumber - x.From.DayNumber < MaxRangeDays)
            .When(x => x.To >= x.From)
            .WithName(nameof(GetRevenueReportRequest.To))
            .WithMessage($"Khoảng thời gian tối đa {MaxRangeDays} ngày.");
    }
}
