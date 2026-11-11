namespace WaterbusSystem.Application.DTOs.Responses.Refunds;

/// <summary>Một refund Manual đang chờ nhân viên hoàn: dùng cho dòng CSV và danh sách chờ trên giao diện. Chứa dữ liệu cá nhân của người mua nên chỉ Admin được xem.</summary>
public sealed record PendingManualRefund(
    Guid RefundId, string BookingCode, Guid OrderId,
    string PurchaserName, string PurchaserEmail, string? PurchaserPhone,
    decimal Amount, string Currency, IReadOnlyList<string> TicketCodes,
    string? RouteName, DateTimeOffset? DepartureTime,
    bool IsLatePayment, string? TripStatus,
    string? MerchantReference, string? PaymentGatewayReference, DateTimeOffset? PaidAt,
    string Status, DateTimeOffset RequestedAt, DateTimeOffset? LastExportedAt);

public sealed record RefundExportFile(string FileName, byte[] Content, int RowCount);

public sealed record BatchConfirmItemResult(Guid RefundTransactionId, bool Succeeded, string? Status, string? Error);

public sealed record BatchConfirmManualRefundsResponse(int Confirmed, int Failed, IReadOnlyList<BatchConfirmItemResult> Items);
