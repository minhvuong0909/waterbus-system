namespace WaterbusSystem.Application.DTOs.Responses.Bookings;

public record CheckoutTicketDto(Guid TicketId, string TicketCode, string PassengerName, string SeatCode,
    string Status, string BoardingStatus, decimal PaidFare);
public record CheckoutBookingDto(Guid BookingId, string BookingCode, string PublicBookingId, Guid TripId,
    Guid BoardingCallId, Guid DisembarkingCallId, string Status, decimal QuotedTotal,
    IReadOnlyList<CheckoutTicketDto> Tickets);
public record CheckoutDetailDto(Guid OrderId, string Status, decimal QuotedTotal, DateTimeOffset? ExpiresAt,
    IReadOnlyList<CheckoutBookingDto> Bookings, IReadOnlyList<CheckoutPaymentDto> Payments);
public record CheckoutPaymentDto(Guid TransactionId, string Type, string Status, decimal Amount, bool RequiresReconciliation);
