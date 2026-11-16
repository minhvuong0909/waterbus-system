using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaterbusSystem.Application.DTOs.Requests.Reports;
using WaterbusSystem.Application.DTOs.Responses.Reports;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.WebApi.Filters;

namespace WaterbusSystem.WebApi.Controllers;

/// <summary>Báo cáo cho Admin (BR-CFG-02: phân biệt tiền thực thu và đã hoàn). Chỉ đọc.</summary>
[ServiceFilter(typeof(ValidateRequestFilter))]
[Authorize(Policy = "AdminOnly")]
public class AdminReportsController(IRevenueReportService revenue) : BaseApiController
{
    /// <summary>
    /// Doanh thu theo ngày hoặc tháng (giờ Việt Nam, tối đa 366 ngày). Thực thu = Payment thành công, đã hoàn = Refund thành công,
    /// thuần = thực thu − đã hoàn; tính theo ngày giao dịch hoàn tất. Không gồm tiền về muộn đang chờ đối soát và refund mô phỏng cũ.
    /// </summary>
    [HttpGet("~/api/v1/admin/reports/revenue")]
    [ProducesResponseType(typeof(RevenueReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RevenueReportResponse>> Revenue([FromQuery] GetRevenueReportRequest request) =>
        Ok(await revenue.GetAsync(request, HttpContext.RequestAborted));
}
