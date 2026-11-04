namespace WaterbusSystem.Application.DTOs.Responses.Trips;

public record TripBoatResponse(Guid BoatId, string Code, string Name);

public record TripCallResponse(Guid CallId, Guid StationId, string StationName, int VisitOrder,
    bool CanBoard, bool CanAlight, string Status, DateTimeOffset? EstimatedArrivalTime,
    DateTimeOffset? EstimatedDepartureTime, DateTimeOffset? CheckInOpenAt, DateTimeOffset? CheckInCloseAt);

public record TripDetailResponse(Guid TripId, Guid RouteId, string RouteName, string TripType, string Status,
    DateTimeOffset DepartureTime, DateTimeOffset ArrivalTime, DateTimeOffset? SalesCloseAt,
    bool IsDelayed, bool CanBook, string SalesState, TripBoatResponse? Boat, IReadOnlyList<TripCallResponse> Stops);
