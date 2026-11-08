using FluentValidation;
using WaterbusSystem.Application.DTOs.Requests.Schedules;

namespace WaterbusSystem.Application.Validators.Schedules;

internal static class ScheduleRules
{
    public static readonly string[] TripTypes = ["Commuter", "Sightseeing"];
    public static readonly string[] Directions = ["Outbound", "Inbound"];

    /// <summary>Chuẩn hoá "1,3,5" thành tập ngày ISO (1..7); null nếu sai định dạng/trùng/rỗng.</summary>
    public static SortedSet<int>? ParseDays(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var set = new SortedSet<int>();
        foreach (var part in value.Split(','))
        {
            if (!int.TryParse(part.Trim(), out var d) || d is < 1 or > 7 || !set.Add(d)) return null;
        }
        return set;
    }
}

public class ScheduleStopInputValidator : AbstractValidator<ScheduleStopInput>
{
    public ScheduleStopInputValidator()
    {
        RuleFor(x => x.RouteStopId).NotEmpty();
        RuleFor(x => x.ArrivalOffsetMin).InclusiveBetween(0, 1440).When(x => x.ArrivalOffsetMin.HasValue);
        RuleFor(x => x.DepartureOffsetMin).InclusiveBetween(0, 1440).When(x => x.DepartureOffsetMin.HasValue);
    }
}

public class CreateScheduleRequestValidator : AbstractValidator<CreateScheduleRequest>
{
    public CreateScheduleRequestValidator()
    {
        RuleFor(x => x.RouteId).NotEmpty();
        RuleFor(x => x.TripType).Must(t => ScheduleRules.TripTypes.Contains(t)).WithMessage("TripType phải là Commuter hoặc Sightseeing.");
        RuleFor(x => x.Direction).Must(d => ScheduleRules.Directions.Contains(d)).WithMessage("Direction phải là Outbound hoặc Inbound.");
        RuleFor(x => x.DepartureTime).Must(t => t >= TimeSpan.Zero && t < TimeSpan.FromDays(1)).WithMessage("Giờ khởi hành phải trong ngày.");
        RuleFor(x => x.DaysOfWeek).Must(d => ScheduleRules.ParseDays(d) != null).WithMessage("DaysOfWeek dạng ISO '1,2,...,7' không trùng lặp.");
        RuleFor(x => x.EffectiveTo).GreaterThanOrEqualTo(x => x.EffectiveFrom).When(x => x.EffectiveTo.HasValue);
        RuleFor(x => x.Stops).NotNull().Must(s => s is { Count: >= 2 and <= 50 }).WithMessage("Lịch cần từ 2 đến 50 điểm dừng.");
        RuleForEach(x => x.Stops).SetValidator(new ScheduleStopInputValidator());
    }
}

public class UpdateScheduleRequestValidator : AbstractValidator<UpdateScheduleRequest>
{
    public UpdateScheduleRequestValidator()
    {
        RuleFor(x => x.TripType).Must(t => ScheduleRules.TripTypes.Contains(t));
        RuleFor(x => x.Direction).Must(d => ScheduleRules.Directions.Contains(d));
        RuleFor(x => x.DepartureTime).Must(t => t >= TimeSpan.Zero && t < TimeSpan.FromDays(1));
        RuleFor(x => x.DaysOfWeek).Must(d => ScheduleRules.ParseDays(d) != null).WithMessage("DaysOfWeek dạng ISO '1,2,...,7' không trùng lặp.");
        RuleFor(x => x.EffectiveTo).GreaterThanOrEqualTo(x => x.EffectiveFrom).When(x => x.EffectiveTo.HasValue);
    }
}

public class ReplaceScheduleStopsRequestValidator : AbstractValidator<ReplaceScheduleStopsRequest>
{
    public ReplaceScheduleStopsRequestValidator()
    {
        RuleFor(x => x.Stops).NotNull().Must(s => s is { Count: >= 2 and <= 50 }).WithMessage("Lịch cần từ 2 đến 50 điểm dừng.");
        RuleForEach(x => x.Stops).SetValidator(new ScheduleStopInputValidator());
    }
}

public class GetSchedulesRequestValidator : AbstractValidator<GetSchedulesRequest>
{
    public GetSchedulesRequestValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
