namespace WaterbusSystem.Application.DTOs.Responses.Refunds;

public sealed record RequestRefundResultDto(
    Guid RefundTransactionId,
    Guid OrderId,
    Guid BookingId,
    decimal Amount,
    string Currency,
    string Status,
    string Gateway,
    bool RequiresManualAction,
    IReadOnlyList<RefundTicketAllocationDto> Tickets);

public sealed record RefundTicketAllocationDto(Guid TicketId, string TicketCode, decimal Amount, string Reason);

public sealed record ConfirmManualRefundResultDto(
    Guid RefundTransactionId, decimal Amount, string Status, DateTimeOffset? CompletedAt);
