namespace WaterbusSystem.Application.DTOs.Responses.Trips;

/// <summary>Chuyến sau thao tác, đủ để màn hình vận hành cập nhật mà không phải tải lại danh sách.</summary>
public record TripOperationResultResponse(
    Guid TripId,
    Guid? BoatId,
    string Status,
    string PreviousStatus,
    string Operation,
    DateTimeOffset? ActualDepartureTime,
    DateTimeOffset? ActualArrivalTime,
    IReadOnlyList<string> AllowedOperations);
