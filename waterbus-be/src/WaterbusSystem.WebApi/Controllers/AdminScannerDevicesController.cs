using Microsoft.AspNetCore.Authorization;
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
public class AdminScannerDevicesController(IScannerDeviceAdminService service) : BaseApiController
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
    public async Task<ActionResult<Guid>> Register([FromBody] RegisterScannerDeviceRequest request)
    {
        var id = await service.RegisterAsync(request, HttpContext.RequestAborted);
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
        await service.DeactivateAsync(id, HttpContext.RequestAborted);
        return NoContent();
    }

    [HttpPost("~/api/v1/admin/scanner-devices/{id:guid}/activate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Activate(Guid id)
    {
        await service.ActivateAsync(id, HttpContext.RequestAborted);
        return NoContent();
    }
}
