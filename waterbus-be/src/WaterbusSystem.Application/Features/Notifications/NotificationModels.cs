using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Application.Features.Notifications;

public sealed record NotificationRecipient(Guid OrderId, Guid? BookingId, string Name, string Email);

public sealed record PassengerLine(string PassengerName, string SeatCode, string SeatClass, string TicketCode, decimal Amount);

/// <summary>Một chặng = một Booking = một Trip (dữ liệu đã commit).</summary>
public sealed record BookingLeg(
    Guid BookingId, string BookingCode, BookingStatus BookingStatus, decimal PaidAmount,
    string PublicBookingId, int QrCredentialVersion, DateTimeOffset QrIssuedAt,
    Guid TripId, TripStatus TripStatus, string RouteName, string FromStation, string ToStation,
    DateTimeOffset Departure, DateTimeOffset Arrival, IReadOnlyList<PassengerLine> Passengers);

public sealed record BookingConfirmationData(
    Guid OrderId, string PurchaserName, string PurchaserEmail, OrderStatus OrderStatus, PurchaseMode PurchaseMode,
    decimal Total, string Currency, IReadOnlyList<BookingLeg> Legs);

public sealed record TripDisruptionData(
    Guid OrderId, string PurchaserName, string PurchaserEmail, string Currency, BookingLeg Leg);

public sealed record RefundSuccessData(
    Guid RefundId, Guid OrderId, string PurchaserName, string PurchaserEmail, FinancialTransactionStatus Status,
    decimal Amount, decimal PaymentAmount, string Currency, string? GatewayReference, DateTimeOffset? CompletedAt,
    BookingLeg Leg, IReadOnlyList<PassengerLine> Allocations, bool IsDemo = false);

public sealed record InlineImage(string ContentId, byte[] Content, string MediaType, string FileName);

/// <summary>Email hoàn chỉnh để gửi. <see cref="MessageKey"/> (= outbox id) tạo Message-Id ổn định giữa các lần retry.</summary>
public sealed record OutboundEmail(
    Guid MessageKey, string ToEmail, string ToName, string Subject, string HtmlBody, string TextBody,
    IReadOnlyList<InlineImage> Images);

public sealed record EmailSendResult(string? ProviderMessageId);

public sealed record ComposeResult(OutboundEmail? Email, string? SkipReason)
{
    public static ComposeResult Ready(OutboundEmail email) => new(email, null);
    public static ComposeResult Skip(string reason) => new(null, reason);
}

public sealed record EmailOutboxBatchResult(int Claimed, int Sent, int Retried, int DeadLettered, int Skipped);

/// <summary>Lỗi gửi do transport. IsTransient=false ⇒ provider từ chối vĩnh viễn (không retry).</summary>
public sealed class EmailDeliveryException(string message, bool isTransient, Exception? inner = null)
    : Exception(message, inner)
{
    public bool IsTransient { get; } = isTransient;
}
