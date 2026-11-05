namespace WaterbusSystem.Application.DTOs.Responses.Trips;

/// <summary>Trạng thái điểm dừng sau khi ghi nhận tiến độ (Planned/Arrived/Departed/Skipped).</summary>
public record StopProgressResultResponse(
    Guid TripStopCallId,
    string Status,
    DateTimeOffset? ActualArrivalTime,
    DateTimeOffset? ActualDepartureTime);
