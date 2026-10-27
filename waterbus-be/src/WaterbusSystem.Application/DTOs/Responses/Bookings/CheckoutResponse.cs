namespace WaterbusSystem.Application.DTOs.Responses.Bookings;

public sealed record CheckoutResponse(
    Guid OrderId,
    decimal QuotedTotal,
    string Currency,
    string Status,
    DateTimeOffset? ExpiresAt,
    string? AccessToken,
    IReadOnlyList<BookingResponse> Bookings
);
