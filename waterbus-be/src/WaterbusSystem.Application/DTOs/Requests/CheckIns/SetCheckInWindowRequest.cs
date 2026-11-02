namespace WaterbusSystem.Application.DTOs.Requests.CheckIns;

/// <summary>Không có TripStopCallId — lấy từ route (`{tripStopCallId:guid}`).</summary>
public record SetCheckInWindowRequest(DateTimeOffset CheckInOpenAt, DateTimeOffset CheckInCloseAt);
