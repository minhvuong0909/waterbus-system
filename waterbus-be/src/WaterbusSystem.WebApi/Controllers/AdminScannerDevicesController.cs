using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.ScannerDevices;
using WaterbusSystem.Application.DTOs.Responses.ScannerDevices;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.WebApi.Filters;

namespace WaterbusSystem.WebApi.Controllers;

/// <summary>Đăng ký và quản lý thiết bị Staff Scanner (chỉ Admin). Gán/thu hồi phân công vẫn ở CheckInsController.</summary>
[ServiceFilter(typeof(ValidateRequestFilter))]
[Authorize(Policy = "AdminOnly")]
public class AdminScannerDevicesController(IScannerDeviceAdminService service, IStationScannerService stationScanners) : BaseApiController
{
    [HttpGet("~/api/v1/admin/scanner-devices")]
    [ProducesResponseType(typeof(PaginatedList<ScannerDeviceResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedList<ScannerDeviceResponse>>> List([FromQuery] GetScannerDevicesRequest request) =>
        Ok(await service.GetPageAsync(request, HttpContext.RequestAborted));

    [HttpGet("~/api/v1/admin/scanner-devices/{id:guid}")]
    [ProducesResponseType(typeof(ScannerDeviceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ScannerDeviceResponse>> Get(Guid id) =>
        Ok(await service.GetByIdAsync(id, HttpContext.RequestAborted));

    [HttpPost("~/api/v1/admin/scanner-devices")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Guid>> Register([FromBody] CreateStationScannerRequest request)
    {
        var id = await stationScanners.CreateAsync(request, HttpContext.RequestAborted);
        return CreatedAtAction(nameof(Get), new { id }, id);
    }

    [HttpPut("~/api/v1/admin/scanner-devices/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateScannerDeviceRequest request)
    {
        await service.UpdateAsync(id, request, HttpContext.RequestAborted);
        return NoContent();
    }

    /// <summary>Ngừng thiết bị; 409 nếu còn phân công chưa kết thúc.</summary>
    [HttpPost("~/api/v1/admin/scanner-devices/{id:guid}/deactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        await stationScanners.SuspendAsync(id, HttpContext.RequestAborted);
        return NoContent();
    }

    [HttpPost("~/api/v1/admin/scanner-devices/{id:guid}/activate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Activate(Guid id)
    {
        await stationScanners.ResetAsync(id, HttpContext.RequestAborted);
        return NoContent();
    }

    [HttpPost("~/api/v1/admin/scanner-devices/{id:guid}/invitation")]
    [EnableRateLimiting("ScannerPairingPolicy")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<ScannerInvitationResponse>> Invite(Guid id) =>
        Ok(await stationScanners.InviteAsync(id, HttpContext.RequestAborted));

    [HttpDelete("~/api/v1/admin/scanner-devices/{id:guid}/invitation")]
    public async Task<IActionResult> CancelInvitation(Guid id)
    {
        await stationScanners.CancelInvitationAsync(id, HttpContext.RequestAborted);
        return NoContent();
    }

    [HttpPost("~/api/v1/admin/scanner-devices/{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid id)
    {
        await stationScanners.RevokeAsync(id, HttpContext.RequestAborted);
        return NoContent();
    }

    [HttpPost("~/api/v1/admin/scanner-devices/{id:guid}/transfer")]
    public async Task<IActionResult> Transfer(Guid id, [FromBody] TransferScannerRequest request)
    {
        await stationScanners.TransferAsync(id, request, HttpContext.RequestAborted);
        return NoContent();
    }

    [HttpPost("~/api/v1/admin/scanner-devices/{id:guid}/audit/{eventId:guid}/review")]
    public async Task<IActionResult> Review(Guid id, Guid eventId, [FromBody] ResolveScannerReviewRequest request)
    {
        await stationScanners.ResolveReviewAsync(id, eventId, request, HttpContext.RequestAborted);
        return NoContent();
    }

    [HttpGet("~/api/v1/admin/scanner-devices/{id:guid}/audit")]
    public async Task<ActionResult<PaginatedList<ScannerAuditResponse>>> Audit(Guid id,
        [FromQuery, Range(1, 1000000)] int pageNumber = 1, [FromQuery, Range(1, 100)] int pageSize = 20) =>
        Ok(await stationScanners.ListAuditAsync(id, pageNumber, pageSize, HttpContext.RequestAborted));
}
