using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaterbusSystem.Application.DTOs.Requests.SeatInventory;
using WaterbusSystem.Application.DTOs.Responses.SeatInventory;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.WebApi.Filters;

namespace WaterbusSystem.WebApi.Controllers;

/// <summary>Quản lý hạng ghế (chỉ Admin).</summary>
[ServiceFilter(typeof(ValidateRequestFilter))]
[Authorize(Policy = "AdminOnly")]
public class SeatClassesController(ISeatClassService service) : BaseApiController
{
    [HttpGet("~/api/v1/seat-classes")]
    [ProducesResponseType(typeof(List<SeatClassResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SeatClassResponse>>> List([FromQuery] GetSeatClassesRequest request) =>
        Ok(await service.ListAsync(request, HttpContext.RequestAborted));

    [HttpGet("~/api/v1/seat-classes/{id:guid}")]
    [ProducesResponseType(typeof(SeatClassResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeatClassResponse>> Get(Guid id) =>
        Ok(await service.GetByIdAsync(id, HttpContext.RequestAborted));

    [HttpPost("~/api/v1/seat-classes")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Guid>> Create([FromBody] CreateSeatClassRequest request)
    {
        var id = await service.CreateAsync(request, HttpContext.RequestAborted);
        return CreatedAtAction(nameof(Get), new { id }, id);
    }

    [HttpPut("~/api/v1/seat-classes/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSeatClassRequest request)
    {
        await service.UpdateAsync(id, request, HttpContext.RequestAborted);
        return NoContent();
    }

    /// <summary>Ngừng hạng ghế; 409 nếu ghế của hạng đang được đặt/giữ trên chuyến chưa kết thúc.</summary>
    [HttpPost("~/api/v1/seat-classes/{id:guid}/deactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        await service.DeactivateAsync(id, HttpContext.RequestAborted);
        return NoContent();
    }

    [HttpPost("~/api/v1/seat-classes/{id:guid}/activate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Activate(Guid id)
    {
        await service.ActivateAsync(id, HttpContext.RequestAborted);
        return NoContent();
    }
}
