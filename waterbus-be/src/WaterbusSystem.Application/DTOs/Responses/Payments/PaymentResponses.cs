using System.Text.Json.Serialization;

namespace WaterbusSystem.Application.DTOs.Responses.Payments;

public record CreateVnPayUrlResponseDto(string PaymentUrl, string OrderId, string MerchantReference, decimal Amount, DateTimeOffset ExpiresAt);
public record VnPayIpnResultDto([property: JsonPropertyName("RspCode")] string RspCode,
    [property: JsonPropertyName("Message")] string Message);
public record VnPayReturnDto(bool Success, string MerchantReference, string ResponseCode, string Message)
{
    public string BookingCode => MerchantReference;
}
