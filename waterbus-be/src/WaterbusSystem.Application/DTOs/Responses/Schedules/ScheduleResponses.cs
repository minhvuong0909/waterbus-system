namespace WaterbusSystem.Application.DTOs.Responses.Schedules;

public record ScheduleStopResponse(
    Guid Id, Guid RouteStopId, int VisitOrder, int SequenceNo, Guid StationId, string StationName,
    int? ArrivalOffsetMin, int? DepartureOffsetMin);

public record ScheduleListItemResponse(
    Guid Id, Guid RouteId, string RouteCode, string RouteName, string TripType, string Direction, TimeSpan DepartureTime,
    string DaysOfWeek, DateOnly EffectiveFrom, DateOnly? EffectiveTo, bool IsActive, int StopCount);

/// <summary><paramref name="StructureLocked"/>: lịch đã sinh Trip nên không đổi điểm dừng/loại chuyến được.</summary>
public record ScheduleDetailResponse(
    Guid Id, Guid RouteId, string RouteCode, string RouteName, string TripType, string Direction, TimeSpan DepartureTime,
    string DaysOfWeek, DateOnly EffectiveFrom, DateOnly? EffectiveTo, bool IsActive, bool StructureLocked, List<ScheduleStopResponse> Stops);
