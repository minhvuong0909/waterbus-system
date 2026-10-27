namespace WaterbusSystem.Application.DTOs.Requests.Bookings;

public record CreateBookingRequest(
    Guid TripId,
    List<Guid> SeatIds,
    Guid BoardingStationId,
    Guid DisembarkingStationId,
    string CustomerName,
    string CustomerEmail,
    string? CustomerPhone,
    IReadOnlyList<CreateCheckoutPassengerRequest>? Passengers = null,
    Guid? BoardingCallId = null,
    Guid? DisembarkingCallId = null);
