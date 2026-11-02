namespace WaterbusSystem.Application.DTOs.Responses.CheckIns;

public record CheckInEventSyncResultResponse(string ClientEventId, string Outcome, bool WasAlreadySynced, string? Error);

public record SyncCheckInEventsResponse(IReadOnlyList<CheckInEventSyncResultResponse> Results);
