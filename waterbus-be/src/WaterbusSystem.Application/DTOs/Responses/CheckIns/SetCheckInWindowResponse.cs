namespace WaterbusSystem.Application.DTOs.Responses.CheckIns;

public record SetCheckInWindowResponse(Guid TripStopCallId, DateTimeOffset CheckInOpenAt, DateTimeOffset CheckInCloseAt);
