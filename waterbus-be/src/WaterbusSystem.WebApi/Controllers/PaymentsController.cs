using Microsoft.AspNetCore.Mvc;
using WaterbusSystem.Application.Features.Payments.Commands.ConfirmVnPayIpn;
using WaterbusSystem.Application.Features.Payments.Commands.CreateVnPayUrl;

namespace WaterbusSystem.WebApi.Controllers;

public record CreateVnPayUrlRequest(Guid BookingId);

/// <summary>
/// Quản lý thanh toán qua cổng VNPAY có chữ ký số HMAC-SHA512
/// </summary>
public class PaymentsController : BaseApiController
{
    /// <summary>
    /// Sinh URL chuyển hướng khách hàng sang cổng thanh toán VNPAY
    /// </summary>
    [HttpPost("create-vnpay-url")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateVnPayUrl([FromBody] CreateVnPayUrlRequest request)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

        var result = await Mediator.Send(new CreateVnPayUrlCommand(request.BookingId, ipAddress));

        return Ok(result);
    }

    /// <summary>
    /// Webhook IPN nhận kết quả thanh toán ngầm từ VNPAY (Xác thực HMAC-SHA512 an toàn)
    /// </summary>
    [HttpGet("vnpay-ipn")]
    public async Task<IActionResult> VnPayIpn()
    {
        var result = await Mediator.Send(new ConfirmVnPayIpnCommand(ToQueryDictionary(Request.Query)));

        return Ok(result);
    }

    private static IReadOnlyDictionary<string, string> ToQueryDictionary(IQueryCollection query) =>
        query.ToDictionary(q => q.Key, q => q.Value.ToString());

    /// <summary>
    /// Xử lý callback khi trình duyệt khách hàng chuyển hướng trở lại sau thanh toán
    /// </summary>
    [HttpGet("vnpay-return")]
    public IActionResult VnPayReturn([FromServices] Application.Common.Interfaces.IVnPayService vnPayService)
    {
        var isValid = vnPayService.ValidateSignature(ToQueryDictionary(Request.Query));
        var responseCode = Request.Query["vnp_ResponseCode"].ToString();
        var bookingCode = Request.Query["vnp_TxnRef"].ToString();

        return Ok(new
        {
            success = isValid && responseCode == "00",
            bookingCode,
            responseCode,
            message = responseCode == "00" ? "Thanh toán thành công" : "Giao dịch không thành công hoặc bị hủy"
        });
    }
}
