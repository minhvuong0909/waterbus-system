using WaterbusSystem.Application.DTOs.Requests.Tickets;
using WaterbusSystem.Application.DTOs.Responses.Tickets;

namespace WaterbusSystem.Application.Interfaces.Services;

/// <summary>
/// Tra cứu lại vé của người mua không cần tài khoản: mã đặt chỗ + email, rồi chứng minh giữ hộp thư bằng OTP.
/// Khớp thông tin chỉ cho phép GỬI OTP; vé và QR chỉ trả sau khi OTP đúng.
/// </summary>
public interface ITicketLookupService
{
    /// <summary>Luôn trả cùng một phản hồi dù thông tin khớp hay không.</summary>
    Task<TicketLookupOtpResponse> RequestOtpAsync(RequestTicketLookupOtpRequest request, CancellationToken ct = default);

    /// <summary>Mọi lý do thất bại (sai OTP, hết hạn, bị khoá, không khớp) đều ném cùng một ForbiddenAccessException.</summary>
    Task<TicketLookupResponse> VerifyOtpAsync(VerifyTicketLookupOtpRequest request, CancellationToken ct = default);
}
