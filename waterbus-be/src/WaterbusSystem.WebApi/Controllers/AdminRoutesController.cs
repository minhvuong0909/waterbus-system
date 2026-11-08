using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.Routes;
using WaterbusSystem.Application.DTOs.Responses.Routes;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.WebApi.Filters;

namespace WaterbusSystem.WebApi.Controllers;

/// <summary>Quản lý Route và RouteStop (chỉ Admin, BR-CFG-01).</summary>
[ServiceFilter(typeof(ValidateRequestFilter))]
[Authorize(Policy = "AdminOnly")]
public class AdminRoutesController(IRouteAdminService service) : BaseApiController
{
    [HttpGet("~/api/v1/admin/routes")]
    [ProducesResponseType(typeof(PaginatedList<RouteListItemResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedList<RouteListItemResponse>>> List([FromQuery] GetRoutesRequest request) =>
        Ok(await service.GetPageAsync(request, HttpContext.RequestAborted));

    [HttpGet("~/api/v1/admin/routes/{id:guid}")]
    [ProducesResponseType(typeof(RouteDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RouteDetailResponse>> Get(Guid id) =>
        Ok(await service.GetByIdAsync(id, HttpContext.RequestAborted));

    [HttpPost("~/api/v1/admin/routes")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Guid>> Create([FromBody] CreateRouteRequest request)
    {
        var id = await service.CreateAsync(request, HttpContext.RequestAborted);
        return CreatedAtAction(nameof(Get), new { id }, id);
    }

    [HttpPut("~/api/v1/admin/routes/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRouteRequest request)
    {
        await service.UpdateAsync(id, request, HttpContext.RequestAborted);
        return NoContent();
    }

    /// <summary>Thay toàn bộ danh sách điểm dừng; 409 nếu Route đã có Schedule/Trip.</summary>
    [HttpPut("~/api/v1/admin/routes/{id:guid}/stops")]
    [ProducesResponseType(typeof(RouteDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RouteDetailResponse>> ReplaceStops(Guid id, [FromBody] ReplaceRouteStopsRequest request) =>
        Ok(await service.ReplaceStopsAsync(id, request, HttpContext.RequestAborted));

    [HttpPost("~/api/v1/admin/routes/{id:guid}/deactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        await service.DeactivateAsync(id, HttpContext.RequestAborted);
        return NoContent();
    }

    [HttpPost("~/api/v1/admin/routes/{id:guid}/activate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Activate(Guid id)
    {
        await service.ActivateAsync(id, HttpContext.RequestAborted);
        return NoContent();
    }
}
