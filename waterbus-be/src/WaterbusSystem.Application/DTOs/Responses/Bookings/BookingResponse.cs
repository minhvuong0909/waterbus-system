namespace WaterbusSystem.Application.DTOs.Responses.Bookings;

public sealed record BookingResponse(
    Guid BookingId,
    string BookingCode,
    decimal TotalAmount,
    string Status,
    int HoldDurationSeconds,
    Guid OrderId = default,
    string OrderStatus = "",
    Guid BoardingCallId = default,
    Guid DisembarkingCallId = default,
    int BoardingStopOrder = 0,
    int DisembarkingStopOrder = 0,
    string? CheckoutSessionId = null,
    string? AccessToken = null
);
