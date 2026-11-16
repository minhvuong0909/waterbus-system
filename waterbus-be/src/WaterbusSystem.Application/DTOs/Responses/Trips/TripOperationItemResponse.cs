using WaterbusSystem.Application.Features.Trips;

namespace WaterbusSystem.Application.DTOs.Responses.Trips;

public record TripOperationItemResponse(
    Guid Id,
    Guid? ScheduleId,
    Guid RouteId,
    string RouteCode,
    string RouteName,
    Guid? BoatId,
    string? BoatCode,
    string TripType,
    string Status,
    DateTimeOffset DepartureTime,
    DateTimeOffset ArrivalTime,
    DateTimeOffset? ActualDepartureTime,
    DateTimeOffset? ActualArrivalTime,
    bool IsDelayed,
    IReadOnlyList<string> AllowedOperations,
    /// <summary>Trạng thái từng nút của thuyền trưởng kèm lý do khi chưa bấm được; chỉ có cho thuyền trưởng.</summary>
    IReadOnlyList<CaptainActionHint>? CaptainActions = null,
    /// <summary>Chuyến đang có SOS còn mở (Admin chưa xử lý): Captain thấy "đã báo SOS, chờ Admin"; chỉ có cho thuyền trưởng.</summary>
    bool OpenSos = false);
