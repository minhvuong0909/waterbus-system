namespace WaterbusSystem.Application.Features.Bookings.Services;

public sealed record BookingPassengerInput(
    string Name,
    string? Email,
    string? Phone
);

public sealed record BookingCreationInput(
    Guid TripId,
    IReadOnlyList<Guid> SeatIds,
    Guid BoardingStationId,
    Guid DisembarkingStationId,
    string CustomerName,
    string CustomerEmail,
    string? CustomerPhone,
    IReadOnlyList<BookingPassengerInput>? Passengers = null,
    Guid? BoardingCallId = null,
    Guid? DisembarkingCallId = null
);
