namespace WaterbusSystem.Application.DTOs.Responses.Routes;

public record RouteStopResponse(Guid Id, int SequenceNo, Guid StationId, string StationName, bool CanBoard, bool CanAlight);

public record RouteListItemResponse(
    Guid Id, string Code, string Name, string ServiceType, bool IsActive, int EstimatedDurationMinutes, decimal DistanceKm,
    string DepartureStationName, string ArrivalStationName, int StopCount);

/// <summary><paramref name="StructureLocked"/>: đã có Schedule/Trip dùng Route nên không đổi bến/loại dịch vụ được.</summary>
public record RouteDetailResponse(
    Guid Id, string Code, string Name, string ServiceType, bool IsActive, int EstimatedDurationMinutes, decimal DistanceKm,
    bool StructureLocked, List<RouteStopResponse> Stops);
