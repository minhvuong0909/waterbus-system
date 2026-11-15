namespace WaterbusSystem.Application.DTOs.Responses.CheckIns;

public record MyScannerAssignmentResponse(
    Guid AssignmentId,
    Guid TripStopCallId,
    Guid TripId,
    string RouteName,
    string StationName,
    int VisitOrder,
    string TripStatus,
    DateTimeOffset? EstimatedArrivalTime,
    Guid DeviceId,
    string DeviceCode,
    bool IsPrimaryOffline,
    DateTimeOffset ActiveFrom,
    DateTimeOffset? ActiveUntil,
    DateTimeOffset? CheckInOpenAt,
    DateTimeOffset? CheckInCloseAt,
    // Định danh chuyến để Staff phân biệt nhiều chuyến cùng tuyến/cùng bến (thêm vào cuối, không đổi thứ tự cũ).
    DateTimeOffset TripDepartureTime = default,
    string? BoatCode = null,
    string? BoatName = null,
    DateTimeOffset? EstimatedDepartureTime = null);
