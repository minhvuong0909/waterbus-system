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
public class RefundsController(IRefundRequestService refunds, IManualRefundService manual, IRefundReconciliationService reconciliation, IRefundBatchService batch, IRefundExportService export) : BaseApiController
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
            IpAddress = ClientIp(),
            CreatedBy = User.Identity?.Name ?? User.FindFirst(ClaimTypes.Email)?.Value
        };

        var result = await refunds.RequestAsync(commandWithContext, HttpContext.RequestAborted);
        return Ok(result);
    }

    /// <summary>
    /// Xem trước (chỉ đọc) các Booking của chuyến bị hủy/chấm dứt cần hoàn tiền: vé đủ điều kiện theo BR-RF-01..09 và chưa
    /// nằm trong refund nào. Chuyến chưa bị hủy/chấm dứt trả 409.
    /// </summary>
    [HttpGet("candidates")]
    [ServiceFilter(typeof(ValidateRequestFilter))]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Candidates([FromQuery] ListRefundCandidatesRequest request) =>
        Ok(await batch.ListCandidatesAsync(request, HttpContext.RequestAborted));

    /// <summary>
    /// Tạo yêu cầu hoàn tiền cho mọi Booking đủ điều kiện của một chuyến (idempotent: gọi lại không hoàn trùng).
    /// Booking lỗi nghiệp vụ được liệt kê ở skippedItems, không làm hỏng cả lô. Không chuyển tiền thật.
    /// </summary>
    [HttpPost("batch-request")]
    [ServiceFilter(typeof(ValidateRequestFilter))]
    [ProducesResponseType(typeof(BatchRefundRequestResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BatchRefundRequestResultResponse>> BatchRequest([FromBody] BatchRequestRefundsRequest request)
    {
        var withContext = request with
        {
            IpAddress = ClientIp(),
            CreatedBy = User.Identity?.Name ?? User.FindFirst(ClaimTypes.Email)?.Value
        };
        return Ok(await batch.RequestForTripAsync(withContext, HttpContext.RequestAborted));
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
    /// <summary>
    /// BR-RF-10: xác nhận đã hoàn nhiều khoản một lần (tối đa 100). Mỗi khoản được kiểm tra và ghi độc lập như
    /// <c>confirm-manual</c>; khoản lỗi nằm ở <c>items[].error</c>, không làm mất các khoản đã xác nhận.
    /// </summary>
    [HttpPost("confirm-manual/batch")]
    [ServiceFilter(typeof(ValidateRequestFilter))]
    [ProducesResponseType(typeof(BatchConfirmManualRefundsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BatchConfirmManualRefundsResponse>> ConfirmManualBatch(
        [FromBody] BatchConfirmManualRefundsRequest request) =>
        Ok(await manual.ConfirmBatchAsync(request, HttpContext.RequestAborted));

    /// <summary>
    /// Danh sách chỉ đọc (phân trang, tối đa 100/trang) các refund Manual đang chờ nhân viên hoàn, cùng tiêu chí với CSV nhưng không ghi dấu
    /// "đã xuất". Dùng để Admin chọn các khoản đã hoàn rồi xác nhận hàng loạt. Chứa dữ liệu cá nhân nên chỉ Admin.
    /// </summary>
    [HttpGet("pending")]
    [ServiceFilter(typeof(ValidateRequestFilter))]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Pending([FromQuery] ListPendingRefundsRequest request) =>
        Ok(await export.ListPendingAsync(request, HttpContext.RequestAborted));

    /// <summary>
    /// Xuất CSV (UTF-8 có BOM) các refund Manual đang chờ nhân viên hoàn tiền: người mua, số tiền, mã vé, mã giao dịch VNPay để hoàn qua
    /// cổng merchant. Ghi dấu "đã xuất" nhưng cho phép xuất lại. Chứa dữ liệu cá nhân nên chỉ Admin. Quá 5000 dòng trả 409: lọc theo tripId.
    /// </summary>
    [HttpPost("export")]
    [ServiceFilter(typeof(ValidateRequestFilter))]
    [Produces("text/csv")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Export([FromBody] ExportRefundsRequest request)
    {
        var file = await export.ExportAsync(request, HttpContext.RequestAborted);
        Response.Headers["X-Row-Count"] = file.RowCount.ToString();
        return File(file.Content, "text/csv; charset=utf-8", file.FileName);
    }

    /// <summary>
    /// Danh sách chỉ đọc các khoản Admin cần đối soát (BR-PAY-05, BR-RF-07): tiền về muộn/xung đột kết quả và refund VNPAY còn Processing.
    /// Với LatePayment chưa có refund, Admin gọi POST request với bookingId trong mục để mở đường hoàn tiền.
    /// </summary>
    [HttpGet("reconciliation-required")]
    public async Task<IActionResult> ReconciliationRequired([FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
        Ok(await reconciliation.ListPendingAsync(page, pageSize, HttpContext.RequestAborted));

    [HttpPost("{refundId:guid}/reconcile")]
    public async Task<IActionResult> Reconcile(Guid refundId) => Ok(new { status = await reconciliation.ReconcileAsync(refundId, ClientIp(), HttpContext.RequestAborted) });
}
