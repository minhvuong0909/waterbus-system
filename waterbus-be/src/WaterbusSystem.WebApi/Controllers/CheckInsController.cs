using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaterbusSystem.Application.DTOs.Requests.CheckIns;
using WaterbusSystem.Application.DTOs.Responses.CheckIns;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.WebApi.Filters;

namespace WaterbusSystem.WebApi.Controllers;

/// <summary>
/// Check-in vé tại bến (Staff Scanner).
/// </summary>
[ServiceFilter(typeof(ValidateRequestFilter))]
public class CheckInsController(ICheckInService checkInService) : BaseApiController
{
    /// <summary>
    /// Staff quét QR và check-in 1 Ticket tại đúng điểm dừng (có mạng).
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "StaffOnly")]
    public async Task<ActionResult<CheckInResultResponse>> SubmitCheckIn([FromBody] SubmitCheckInRequest request) =>
        Ok(await checkInService.SubmitCheckInAsync(request, HttpContext.RequestAborted));

    /// <summary>
    /// Đồng bộ theo lô các sự kiện check-in đã ghi offline (idempotent theo ClientEventId).
    /// </summary>
    [HttpPost("sync")]
    [Authorize(Policy = "StaffOnly")]
    public async Task<ActionResult<SyncCheckInEventsResponse>> SyncCheckInEvents([FromBody] SyncCheckInEventsRequest request) =>
        Ok(await checkInService.SyncCheckInEventsAsync(request, HttpContext.RequestAborted));

    /// <summary>
    /// Staff Scanner preload manifest Ticket của 1 điểm dừng được phân công trước giờ mở cổng.
    /// </summary>
    [HttpGet("manifest/{tripStopCallId:guid}")]
    [Authorize(Policy = "StaffOnly")]
    public async Task<ActionResult<ScannerManifestResponse>> GetScannerManifest(Guid tripStopCallId) =>
        Ok(await checkInService.GetScannerManifestAsync(tripStopCallId, HttpContext.RequestAborted));

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
    public async Task<ActionResult<IReadOnlyList<MyScannerAssignmentResponse>>> GetMyScannerAssignments() =>
        Ok(await checkInService.GetMyScannerAssignmentsAsync(HttpContext.RequestAborted));

    /// <summary>
    /// Admin phân công thiết bị quét + Staff cho một điểm dừng.
    /// </summary>
    [HttpPost("assignments")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<AssignScannerResponse>> AssignScanner([FromBody] AssignScannerRequest request) =>
        Ok(await checkInService.AssignScannerAsync(request, HttpContext.RequestAborted));

    /// <summary>
    /// Admin thu hồi một phân công scanner (vd để giải phóng slot Primary Offline trước khi gán lại).
    /// </summary>
    [HttpPost("assignments/{assignmentId:guid}/revoke")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<RevokeScannerAssignmentResponse>> RevokeScannerAssignment(Guid assignmentId) =>
        Ok(await checkInService.RevokeScannerAssignmentAsync(assignmentId, HttpContext.RequestAborted));
}
