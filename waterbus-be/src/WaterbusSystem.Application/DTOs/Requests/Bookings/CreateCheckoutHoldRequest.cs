namespace WaterbusSystem.Application.DTOs.Requests.Bookings;

public sealed record CreateCheckoutHoldRequest(
    List<CheckoutHoldLegRequest> Legs
);

public sealed record CheckoutHoldLegRequest(
    Guid TripId,
    List<Guid> SeatIds,
    Guid BoardingStationId,
    Guid DisembarkingStationId,
    Guid? BoardingCallId = null,
    Guid? DisembarkingCallId = null
);
