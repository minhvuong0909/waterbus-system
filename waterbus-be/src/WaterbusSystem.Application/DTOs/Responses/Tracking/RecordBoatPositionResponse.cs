namespace WaterbusSystem.Application.DTOs.Responses.Tracking;

public record RecordBoatPositionResponse(
    bool Accepted, 
    bool Persisted, 
    Guid? TripId);
