namespace WaterbusSystem.Application.DTOs.Requests.Trips;

/// <summary>Nhật ký thao tác vận hành (TripOperationEvent), mới nhất trước; có thể lọc theo một chuyến.</summary>
public record GetTripOperationEventsRequest(Guid? TripId = null, int Take = 50);
