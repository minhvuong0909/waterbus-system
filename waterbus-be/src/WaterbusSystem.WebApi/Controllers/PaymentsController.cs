using Microsoft.AspNetCore.Mvc;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Application.DTOs.Requests.Payments;
using WaterbusSystem.Application.Features.Payments.Commands.CreateVnPayUrl;

namespace WaterbusSystem.WebApi.Controllers;



/// <summary>
/// Quản lý thanh toán qua cổng VNPAY có chữ ký số HMAC-SHA512
/// </summary>
public class PaymentsController(IPaymentService service) : BaseApiController
{
    /// <summary>
    /// Sinh URL chuyển hướng khách hàng sang cổng thanh toán VNPAY.
    /// BR-PAY-04: đơn vị thanh toán là PurchaseOrder, nên nhận OrderId chứ không phải BookingId.
    /// </summary>
    [HttpPost("create-vnpay-url")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateVnPayUrl([FromBody] CreateVnPayUrlRequest request,
        [FromHeader(Name = "X-Guest-Token")] string? accessToken)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

        var result = await service.CreateVnPayUrlAsync(request.OrderId, ipAddress, accessToken, HttpContext.RequestAborted);

        return Ok(result);
    }

    /// <summary>
    /// Webhook IPN nhận kết quả thanh toán ngầm từ VNPAY (Xác thực HMAC-SHA512 an toàn).
    /// BR-PAY-01: đây là bằng chứng xác nhận thanh toán duy nhất. Nhận cả GET và POST
    /// để không phụ thuộc phương thức mà gateway chọn.
    /// </summary>
    [HttpGet("vnpay-ipn")]
    [HttpPost("vnpay-ipn")]
    public async Task<IActionResult> VnPayIpn()
    {
        var result = await service.ConfirmIpnAsync(ToQueryDictionary(Request.Query), HttpContext.RequestAborted);

        return Ok(result);
    }

    private static IReadOnlyDictionary<string, string> ToQueryDictionary(IQueryCollection query) =>
        query.ToDictionary(q => q.Key, q => q.Value.ToString());

    /// <summary>
    /// Xử lý callback khi trình duyệt khách hàng chuyển hướng trở lại sau thanh toán
    /// </summary>
    [HttpGet("vnpay-return")]
    public async Task<IActionResult> VnPayReturn() => Ok(await service.GetReturnAsync(ToQueryDictionary(Request.Query), HttpContext.RequestAborted));

    [HttpPost("{orderId:guid}/attempts/{paymentId:guid}/reconcile")]
    public async Task<IActionResult> Reconcile(Guid orderId, Guid paymentId,
        [FromHeader(Name = "X-Guest-Token")] string? accessToken) => Ok(await service.ReconcileAsync(orderId, paymentId,
            accessToken, HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1", HttpContext.RequestAborted));
}
