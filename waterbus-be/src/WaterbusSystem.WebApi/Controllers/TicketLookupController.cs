using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WaterbusSystem.Application.DTOs.Requests.Tickets;
using WaterbusSystem.Application.DTOs.Responses.Tickets;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.WebApi.Filters;

namespace WaterbusSystem.WebApi.Controllers;

/// <summary>
/// Tra cứu lại vé không cần đăng nhập: mã đặt chỗ + email người mua, xác thực bằng OTP gửi về email đó.
/// Công khai nên mọi action đều có rate limit riêng theo IP; chi tiết chống dò nằm ở service.
/// </summary>
[AllowAnonymous]
[Route("api/v1/tickets/lookup")]
public sealed class TicketLookupController(ITicketLookupService lookup) : BaseApiController
{
    /// <summary>Xin OTP. Luôn trả cùng một phản hồi dù thông tin có khớp hay không.</summary>
    [HttpPost("otp")]
    [ServiceFilter(typeof(ValidateRequestFilter))]
    [EnableRateLimiting("TicketLookupPolicy")]
    [ProducesResponseType(typeof(TicketLookupOtpResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<TicketLookupOtpResponse>> RequestOtp([FromBody] RequestTicketLookupOtpRequest request) =>
        Ok(await lookup.RequestOtpAsync(request, HttpContext.RequestAborted));

    /// <summary>Xác minh OTP và trả vé kèm QR. Mọi thất bại cùng 403.</summary>
    [HttpPost("verify")]
    [ServiceFilter(typeof(ValidateRequestFilter))]
    [EnableRateLimiting("TicketLookupPolicy")]
    [ProducesResponseType(typeof(TicketLookupResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<TicketLookupResponse>> Verify([FromBody] VerifyTicketLookupOtpRequest request) =>
        Ok(await lookup.VerifyOtpAsync(request, HttpContext.RequestAborted));
}
