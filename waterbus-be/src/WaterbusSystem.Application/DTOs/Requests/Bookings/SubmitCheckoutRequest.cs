namespace WaterbusSystem.Application.DTOs.Requests.Bookings;

public sealed record CheckoutPassengerRequest(
    Guid BookingId,
    Guid SeatId,
    string Name,
    string? Email,
    string? Phone
);

public sealed record SubmitCheckoutRequest(
    string CustomerName,
    string CustomerEmail,
    string? CustomerPhone,
    IReadOnlyList<CheckoutPassengerRequest>? Passengers = null
);
