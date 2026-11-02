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
    IReadOnlyList<string> AllowedOperations);
