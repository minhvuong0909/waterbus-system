namespace WaterbusSystem.Application.DTOs.Requests.Refunds;

public record RequestRefundRequest(
    Guid BookingId,
    string Reason,
    string? IpAddress = null,
    string? CreatedBy = null);
