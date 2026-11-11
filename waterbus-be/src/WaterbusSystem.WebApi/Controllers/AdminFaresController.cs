using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.Fares;
using WaterbusSystem.Application.DTOs.Responses.Fares;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.WebApi.Filters;

namespace WaterbusSystem.WebApi.Controllers;

/// <summary>Quản lý bảng giá (FareRule) - chỉ Admin. Endpoint công khai xem giá hiện hành vẫn là /api/v1/fares.</summary>
[ServiceFilter(typeof(ValidateRequestFilter))]
[Authorize(Policy = "AdminOnly")]
public class AdminFaresController(IFareAdminService service) : BaseApiController
{
    [HttpGet("~/api/v1/admin/fares")]
    [ProducesResponseType(typeof(PaginatedList<FareRuleAdminResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedList<FareRuleAdminResponse>>> List([FromQuery] GetFareRulesRequest request) =>
        Ok(await service.GetPageAsync(request, HttpContext.RequestAborted));

    [HttpGet("~/api/v1/admin/fares/{id:guid}")]
    [ProducesResponseType(typeof(FareRuleAdminResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FareRuleAdminResponse>> Get(Guid id) =>
        Ok(await service.GetByIdAsync(id, HttpContext.RequestAborted));

    [HttpPost("~/api/v1/admin/fares")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<Guid>> Create([FromBody] CreateFareRuleRequest request)
    {
        var id = await service.CreateAsync(request, HttpContext.RequestAborted);
        return CreatedAtAction(nameof(Get), new { id }, id);
    }

    /// <summary>Bảng giá đã có hiệu lực chỉ đổi được EffectiveTo (409 nếu đổi giá/EffectiveFrom).</summary>
    [HttpPut("~/api/v1/admin/fares/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateFareRuleRequest request)
    {
        await service.UpdateAsync(id, request, HttpContext.RequestAborted);
        return NoContent();
    }

    [HttpPost("~/api/v1/admin/fares/{id:guid}/deactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        await service.DeactivateAsync(id, HttpContext.RequestAborted);
        return NoContent();
    }
}
