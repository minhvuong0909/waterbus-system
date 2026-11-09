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
    IReadOnlyList<RefundTicketAllocationDto> Tickets,
    bool IsDemo = false,
    string? Notice = null);

public sealed record RefundTicketAllocationDto(Guid TicketId, string TicketCode, decimal Amount, string Reason);

public sealed record ConfirmManualRefundResultDto(
    Guid RefundTransactionId, decimal Amount, string Status, DateTimeOffset? CompletedAt,
    bool IsDemo = false, string? Notice = null);

/// <summary>Thông điệp cố định gắn vào mọi phản hồi/thông báo của refund demo.</summary>
public static class RefundDemo
{
    public const string Notice = "DEMO: giao dịch hoàn tiền mô phỏng, KHÔNG có tiền thật được chuyển.";
}
