using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.CheckIns;
using WaterbusSystem.Application.DTOs.Responses.CheckIns;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.WebApi.Filters;
using WaterbusSystem.Application.DTOs.Requests.ScannerDevices;
using WaterbusSystem.Application.Features.ScannerDevices.Services;

namespace WaterbusSystem.WebApi.Controllers;

/// <summary>
/// Check-in vé tại bến (Staff Scanner).
/// </summary>
[ServiceFilter(typeof(ValidateRequestFilter))]
public class CheckInsController(ICheckInService checkInService, StationScannerAccessService scannerAccess, StationScannerPolicy stationPolicy) : BaseApiController
{
    /// <summary>
    /// Staff quét QR và check-in 1 Ticket tại đúng điểm dừng (có mạng).
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "StaffOnly")]
    public async Task<ActionResult<CheckInResultResponse>> SubmitCheckIn([FromBody] SubmitCheckInRequest request, [FromHeader(Name = "X-Scanner-Credential")] string? credential)
    {
        await using var scanner = await scannerAccess.OpenAsync(credential, false, HttpContext.RequestAborted);
        return Ok(await checkInService.SubmitCheckInAsync(request, HttpContext.RequestAborted, scanner));
    }

    /// <summary>
    /// Đồng bộ theo lô các sự kiện check-in đã ghi offline (idempotent theo ClientEventId).
    /// </summary>
    [HttpPost("sync")]
    [Authorize(Policy = "StaffOnly")]
    public async Task<ActionResult<SyncCheckInEventsResponse>> SyncCheckInEvents([FromBody] SyncCheckInEventsRequest request, [FromHeader(Name = "X-Scanner-Credential")] string? credential)
    {
        await using var scanner = await scannerAccess.OpenAsync(credential, true, HttpContext.RequestAborted);
        return Ok(await checkInService.SyncCheckInEventsAsync(request, HttpContext.RequestAborted, scanner));
    }

    /// <summary>
    /// Staff Scanner preload manifest Ticket của 1 điểm dừng được phân công trước giờ mở cổng.
    /// </summary>
    [HttpGet("manifest/{tripStopCallId:guid}")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [Authorize(Policy = "StaffOnly")]
    public async Task<ActionResult<ScannerManifestResponse>> GetScannerManifest(Guid tripStopCallId, [FromHeader(Name = "X-Scanner-Credential")] string? credential)
    {
        await using var scanner = await scannerAccess.OpenAsync(credential, false, HttpContext.RequestAborted);
        return Ok(await checkInService.GetScannerManifestAsync(tripStopCallId, HttpContext.RequestAborted, scanner));
    }

    /// <summary>
    /// Admin cấu hình cửa sổ check-in cho một điểm dừng cụ thể.
    /// </summary>
    [HttpPut("windows/{tripStopCallId:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<SetCheckInWindowResponse>> SetCheckInWindow(Guid tripStopCallId, [FromBody] SetCheckInWindowRequest request) =>
        Ok(await checkInService.SetCheckInWindowAsync(tripStopCallId, request, HttpContext.RequestAborted));

    /// <summary>
    /// Staff xem các phân công soát vé hiện hành/sắp tới của chính mình (điểm dừng nào, bến nào,
    /// cửa sổ check-in) để biết TripStopCallId cần preload manifest và gửi check-in.
    /// </summary>
    [HttpGet("assignments/mine")]
    [Authorize(Policy = "StaffOnly")]
    public async Task<ActionResult<IReadOnlyList<MyScannerAssignmentResponse>>> GetMyScannerAssignments([FromHeader(Name = "X-Scanner-Credential")] string? credential)
    {
        await using var scanner = await scannerAccess.OpenAsync(credential, false, HttpContext.RequestAborted);
        return Ok(await checkInService.GetMyScannerAssignmentsAsync(HttpContext.RequestAborted, scanner));
    }

    /// <summary>Admin chọn nhân viên Staff đang hoạt động để phân công.</summary>
    [HttpGet("assignments/staff")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<PaginatedList<ScannerStaffResponse>>> ListScannerStaff(
        [FromQuery, Range(1, 1000000)] int pageNumber = 1, [FromQuery, Range(1, 100)] int pageSize = 50) =>
        Ok(await checkInService.ListScannerStaffAsync(pageNumber, pageSize, HttpContext.RequestAborted));

    /// <summary>Admin xem phân công hiện hành và sắp tới tại một điểm dừng.</summary>
    [HttpGet("assignments/stops/{tripStopCallId:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<PaginatedList<ScannerAssignmentAdminResponse>>> ListScannerAssignments(
        Guid tripStopCallId, [FromQuery, Range(1, 1000000)] int pageNumber = 1, [FromQuery, Range(1, 100)] int pageSize = 50) =>
        Ok(await stationPolicy.ListPrimaryAssignmentsAsync(tripStopCallId, pageNumber, pageSize, HttpContext.RequestAborted));

    /// <summary>
    /// Admin phân công thiết bị Primary Offline cho một điểm dừng.
    /// </summary>
    [HttpPost("assignments")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<Guid>> AssignScanner([FromBody] CreatePrimaryScannerRequest request) =>
        Ok(await stationPolicy.AssignPrimaryAsync(request, HttpContext.RequestAborted));

    /// <summary>
    /// Admin thu hồi một phân công scanner (vd để giải phóng slot Primary Offline trước khi gán lại).
    /// </summary>
    [HttpPost("assignments/{assignmentId:guid}/revoke")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> RevokeScannerAssignment(Guid assignmentId)
    {
        await stationPolicy.RevokePrimaryAsync(assignmentId, HttpContext.RequestAborted);
        return NoContent();
    }
}
