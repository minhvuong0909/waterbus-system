namespace WaterbusSystem.Application.DTOs.Responses.Refunds;

/// <summary>Một dòng CSV: một refund Manual đang chờ hoàn. Chứa dữ liệu cá nhân của người mua nên chỉ Admin được xuất.</summary>
public sealed record RefundExportRow(
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
