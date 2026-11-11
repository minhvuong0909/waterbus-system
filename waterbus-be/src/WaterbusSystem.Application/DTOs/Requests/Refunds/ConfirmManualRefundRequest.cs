namespace WaterbusSystem.Application.DTOs.Requests.Refunds;

public record ConfirmManualRefundRequest(
    Guid RefundTransactionId,
    string Evidence,
    string? GatewayReference, decimal ConfirmedAmount);
