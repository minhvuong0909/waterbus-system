namespace WaterbusSystem.Application.DTOs.Responses.Trips;

public record TripOperationEventResponse(
    Guid Id,
    Guid TripId,
    Guid? IncidentId,
    string Action,
    string? FromStatus,
    string ToStatus,
    string? Note,
    DateTimeOffset OccurredAt,
    string? ActedBy);
