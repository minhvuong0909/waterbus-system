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
    DateTimeOffset? CheckInCloseAt);
