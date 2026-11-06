namespace WaterbusSystem.Application.DTOs.Requests.Schedules;

/// <summary>Điểm dừng của lịch; VisitOrder = vị trí trong danh sách (từ 1). Offset tính bằng phút từ giờ khởi hành.</summary>
public record ScheduleStopInput(Guid RouteStopId, int? ArrivalOffsetMin, int? DepartureOffsetMin);

/// <summary>
/// DepartureTime là giờ Việt Nam (UTC+7), như dữ liệu seed hiện có. DaysOfWeek theo ISO: 1=Thứ Hai ... 7=Chủ Nhật, ví dụ "1,2,3,4,5".
/// Direction: Outbound hoặc Inbound.
/// </summary>
public record CreateScheduleRequest(
    Guid RouteId, string TripType, string Direction, TimeSpan DepartureTime, string DaysOfWeek,
    DateOnly EffectiveFrom, DateOnly? EffectiveTo, List<ScheduleStopInput> Stops);

/// <summary>Không đổi được RouteId. TripType chỉ đổi được khi lịch chưa sinh Trip nào.</summary>
public record UpdateScheduleRequest(
    string TripType, string Direction, TimeSpan DepartureTime, string DaysOfWeek, DateOnly EffectiveFrom, DateOnly? EffectiveTo);

public record ReplaceScheduleStopsRequest(List<ScheduleStopInput> Stops);

public record GetSchedulesRequest(int PageNumber = 1, int PageSize = 10, Guid? RouteId = null, bool? IsActive = null);
