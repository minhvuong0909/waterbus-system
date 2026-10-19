using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaterbusSystem.Application.Features.CheckIns.Commands.AssignScanner;
using WaterbusSystem.Application.Features.CheckIns.Commands.RevokeScannerAssignment;
using WaterbusSystem.Application.Features.CheckIns.Commands.SetCheckInWindow;
using WaterbusSystem.Application.Features.CheckIns.Commands.SubmitCheckIn;
using WaterbusSystem.Application.Features.CheckIns.Commands.SyncCheckInEvents;
using WaterbusSystem.Application.Features.CheckIns.Queries.GetMyScannerAssignments;
using WaterbusSystem.Application.Features.CheckIns.Queries.GetScannerManifest;

namespace WaterbusSystem.WebApi.Controllers;

/// <summary>
/// Check-in vé tại bến (Staff Scanner).
/// </summary>
public class CheckInsController : BaseApiController
{
    /// <summary>
    /// Staff quét QR và check-in 1 Ticket tại đúng điểm dừng (có mạng).
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> SubmitCheckIn(SubmitCheckInCommand command) =>
        Ok(await Mediator.Send(command, HttpContext.RequestAborted));

    /// <summary>
    /// Đồng bộ theo lô các sự kiện check-in đã ghi offline (idempotent theo ClientEventId).
    /// </summary>
    [HttpPost("sync")]
    [Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> SyncCheckInEvents(SyncCheckInEventsCommand command) =>
        Ok(await Mediator.Send(command, HttpContext.RequestAborted));

    /// <summary>
    /// Staff Scanner preload manifest Ticket của 1 điểm dừng được phân công trước giờ mở cổng.
    /// </summary>
    [HttpGet("manifest/{tripStopCallId:guid}")]
    [Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> GetScannerManifest(Guid tripStopCallId) =>
        Ok(await Mediator.Send(new GetScannerManifestQuery(tripStopCallId), HttpContext.RequestAborted));

    /// <summary>
    /// Admin cấu hình cửa sổ check-in cho một điểm dừng cụ thể.
    /// </summary>
    [HttpPut("windows/{tripStopCallId:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> SetCheckInWindow(Guid tripStopCallId, SetCheckInWindowCommand command) =>
        Ok(await Mediator.Send(command with { TripStopCallId = tripStopCallId }, HttpContext.RequestAborted));

    /// <summary>
    /// Staff xem các phân công soát vé hiện hành/sắp tới của chính mình (điểm dừng nào, bến nào,
    /// cửa sổ check-in) để biết TripStopCallId cần preload manifest và gửi check-in.
    /// </summary>
    [HttpGet("assignments/mine")]
    [Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> GetMyScannerAssignments() =>
        Ok(await Mediator.Send(new GetMyScannerAssignmentsQuery(), HttpContext.RequestAborted));

    /// <summary>
    /// Admin phân công thiết bị quét + Staff cho một điểm dừng.
    /// </summary>
    [HttpPost("assignments")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> AssignScanner(AssignScannerCommand command) =>
        Ok(await Mediator.Send(command, HttpContext.RequestAborted));

    /// <summary>
    /// Admin thu hồi một phân công scanner (vd để giải phóng slot Primary Offline trước khi gán lại).
    /// </summary>
    [HttpPost("assignments/{assignmentId:guid}/revoke")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> RevokeScannerAssignment(Guid assignmentId) =>
        Ok(await Mediator.Send(new RevokeScannerAssignmentCommand(assignmentId), HttpContext.RequestAborted));
}
