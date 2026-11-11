namespace WaterbusSystem.Application.DTOs.Responses.Trips;

/// <summary>
/// Một bến dừng hợp lệ trên hành trình của chuyến tàu, phục vụ FE/Mobile cho khách chọn bến lên/bến xuống
/// </summary>
public record TripSearchStopResponse(Guid StationId, string StationName, int OrderIndex);

/// <summary>
/// DTO thông tin chuyến tàu phục vụ hiển thị Trip Card
/// </summary>
public record TripSearchResponse(
    Guid Id,
    string RouteName,
    string DepartureStationName,
    string ArrivalStationName,
    DateTimeOffset DepartureTime,
    DateTimeOffset ArrivalTime,
    int? DurationMinutes,
    string TripType,
    decimal BasePrice,
    int AvailableSeatsCount,
    List<TripSearchStopResponse> Stops,
    bool CanBook = true,
    string SalesState = "open");
