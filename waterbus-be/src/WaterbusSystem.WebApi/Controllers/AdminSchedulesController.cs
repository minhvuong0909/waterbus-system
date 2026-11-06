using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.Schedules;
using WaterbusSystem.Application.DTOs.Responses.Schedules;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.WebApi.Filters;

namespace WaterbusSystem.WebApi.Controllers;

/// <summary>Quản lý Schedule (chỉ Admin, BR-CFG-01). Không tự sinh Trip - xem endpoint tạo chuyến từ lịch.</summary>
[ServiceFilter(typeof(ValidateRequestFilter))]
[Authorize(Policy = "AdminOnly")]
public class AdminSchedulesController(IScheduleAdminService service) : BaseApiController
{
    [HttpGet("~/api/v1/admin/schedules")]
    [ProducesResponseType(typeof(PaginatedList<ScheduleListItemResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedList<ScheduleListItemResponse>>> List([FromQuery] GetSchedulesRequest request) =>
        Ok(await service.GetPageAsync(request, HttpContext.RequestAborted));

    [HttpGet("~/api/v1/admin/schedules/{id:guid}")]
    [ProducesResponseType(typeof(ScheduleDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ScheduleDetailResponse>> Get(Guid id) =>
        Ok(await service.GetByIdAsync(id, HttpContext.RequestAborted));

    [HttpPost("~/api/v1/admin/schedules")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<Guid>> Create([FromBody] CreateScheduleRequest request)
    {
        var id = await service.CreateAsync(request, HttpContext.RequestAborted);
        return CreatedAtAction(nameof(Get), new { id }, id);
    }

    [HttpPut("~/api/v1/admin/schedules/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateScheduleRequest request)
    {
        await service.UpdateAsync(id, request, HttpContext.RequestAborted);
        return NoContent();
    }

    /// <summary>Thay điểm dừng của lịch; 409 nếu lịch đã sinh Trip.</summary>
    [HttpPut("~/api/v1/admin/schedules/{id:guid}/stops")]
    [ProducesResponseType(typeof(ScheduleDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ScheduleDetailResponse>> ReplaceStops(Guid id, [FromBody] ReplaceScheduleStopsRequest request) =>
        Ok(await service.ReplaceStopsAsync(id, request, HttpContext.RequestAborted));

    [HttpPost("~/api/v1/admin/schedules/{id:guid}/deactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        await service.DeactivateAsync(id, HttpContext.RequestAborted);
        return NoContent();
    }

    [HttpPost("~/api/v1/admin/schedules/{id:guid}/activate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Activate(Guid id)
    {
        await service.ActivateAsync(id, HttpContext.RequestAborted);
        return NoContent();
    }
}
