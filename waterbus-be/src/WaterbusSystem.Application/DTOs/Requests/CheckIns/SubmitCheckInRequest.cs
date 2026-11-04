namespace WaterbusSystem.Application.DTOs.Requests.CheckIns;

public record SubmitCheckInRequest(
    Guid TicketId,
    Guid TripStopCallId,
    string ClientEventId,
    DateTimeOffset OccurredAtDevice,
    string? Note);
