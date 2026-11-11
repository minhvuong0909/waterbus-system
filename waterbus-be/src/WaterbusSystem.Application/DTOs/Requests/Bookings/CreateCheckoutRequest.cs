namespace WaterbusSystem.Application.DTOs.Requests.Bookings;

public sealed record CreateCheckoutRequest(
    string CustomerName,
    string CustomerEmail,
    string? CustomerPhone,
    List<CreateCheckoutLegRequest> Legs);

public sealed record CreateCheckoutLegRequest(
    Guid TripId,
    List<Guid> SeatIds,
    Guid BoardingStationId,
    Guid DisembarkingStationId,
    IReadOnlyList<CreateCheckoutPassengerRequest>? Passengers = null,
    Guid? BoardingCallId = null,
    Guid? DisembarkingCallId = null);

public sealed record CreateCheckoutPassengerRequest(
    string Name,
    string? Email,
    string? Phone);
