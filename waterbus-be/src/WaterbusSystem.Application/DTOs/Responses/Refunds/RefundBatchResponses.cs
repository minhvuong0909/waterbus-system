namespace WaterbusSystem.Application.DTOs.Responses.Refunds;

/// <summary>Một Booking cần hoàn: số tiền là tổng paid allocation của các vé đủ điều kiện, chưa nằm trong refund nào.</summary>
public sealed record RefundCandidateResponse(
    Guid BookingId, string BookingCode, Guid OrderId,
    string PurchaserName, string PurchaserEmail, string? PurchaserPhone,
    decimal Amount, string Currency, IReadOnlyList<string> TicketCodes, string Reason);

public sealed record BatchRefundCreatedItem(Guid BookingId, string BookingCode, Guid RefundTransactionId, decimal Amount, string Status);

public sealed record BatchRefundSkippedItem(Guid BookingId, string BookingCode, string Reason);

/// <summary>Kết quả tạo yêu cầu hoàn hàng loạt: từng Booking hoặc được tạo, hoặc bị bỏ qua kèm lý do (không dừng cả lô).</summary>
public sealed record BatchRefundRequestResultResponse(
    Guid TripId, int Requested, int Skipped,
    IReadOnlyList<BatchRefundCreatedItem> Created, IReadOnlyList<BatchRefundSkippedItem> SkippedItems);
