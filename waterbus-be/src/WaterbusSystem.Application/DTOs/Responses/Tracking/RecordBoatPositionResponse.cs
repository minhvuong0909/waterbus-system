namespace WaterbusSystem.Application.DTOs.Responses.Tracking;

/// <summary>Kết quả ghi nhận vị trí: Accepted = đã phát realtime; Persisted = đã ghi BoatPositionEvent.</summary>
public record RecordBoatPositionResponse(bool Accepted, bool Persisted, Guid? TripId);
