using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Application.DTOs.Requests.Refunds;
using WaterbusSystem.Application.DTOs.Responses.Refunds;
using WaterbusSystem.WebApi.Filters;

namespace WaterbusSystem.WebApi.Controllers;

/// <summary>
/// BR-ROLE-04: Admin quyết định các thao tác vận hành được phân quyền như Cancel, Terminate và
/// xử lý Incident. Việc hoàn tiền cũng thuộc thẩm quyền Admin, không phải hành động tự động của khách.
/// BR-RF-07: Refund có trạng thái quá trình, liên kết payment gốc, chống lặp và cơ chế đối soát.
/// </summary>
[Authorize(Policy = "AdminOnly")]
public class RefundsController(IRefundRequestService refunds, IManualRefundService manual, IRefundReconciliationService reconciliation) : BaseApiController
{
    /// <summary>
    /// Tạo yêu cầu hoàn tiền cho một Booking bị ảnh hưởng bởi Trip bị hủy hoặc chấm dứng.
    /// BR-RF-08: Order round-trip chỉ hoàn phần phân bổ cho Booking đủ điều kiện.
    /// </summary>
    [HttpPost("request")]
    [ServiceFilter(typeof(ValidateRequestFilter))]
    [ProducesResponseType(typeof(RequestRefundResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RequestRefundResultDto>> RequestRefund([FromBody] RequestRefundRequest command)
    {
        // vnp_IpAddr và vnp_CreateBy là hai trường bắt buộc của API hoàn tiền VNPAY,
        // dùng để cổng đối soát và chống gian lận.
        var commandWithContext = command with
        {
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
            CreatedBy = User.Identity?.Name ?? User.FindFirst(ClaimTypes.Email)?.Value
        };

        var result = await refunds.RequestAsync(commandWithContext, HttpContext.RequestAborted);
        return Ok(result);
    }

    /// <summary>
    /// BR-RF-10: Admin xác nhận đã hoàn tiền thủ công khi gateway không hỗ trợ hoặc trả lỗi.
    /// Bắt buộc kèm bằng chứng đối soát; không được xác nhận lần hai.
    /// </summary>
    [HttpPost("confirm-manual")]
    [ServiceFilter(typeof(ValidateRequestFilter))]
    [ProducesResponseType(typeof(ConfirmManualRefundResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ConfirmManualRefundResultDto>> ConfirmManualRefund(
        [FromBody] ConfirmManualRefundRequest command)
    {
        var result = await manual.ConfirmAsync(command, HttpContext.RequestAborted);
        return Ok(result);
    }
    [HttpPost("{refundId:guid}/reconcile")]
    public async Task<IActionResult> Reconcile(Guid refundId) => Ok(new { status = await reconciliation.ReconcileAsync(refundId, HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1", HttpContext.RequestAborted) });
}
