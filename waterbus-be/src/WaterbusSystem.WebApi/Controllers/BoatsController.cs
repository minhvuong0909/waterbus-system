using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.Boats;
using WaterbusSystem.Application.DTOs.Responses.Boats;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.WebApi.Filters;

namespace WaterbusSystem.WebApi.Controllers;

/// <summary>
/// Quản lý danh mục tàu thủy chở khách (Boats)
/// </summary>
[ServiceFilter(typeof(ValidateRequestFilter))]
public class BoatsController(IBoatService boatService) : BaseApiController
{
    /// <summary>
    /// Lấy danh sách tàu có phân trang
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedList<BoatResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedList<BoatResponse>>> GetBoats([FromQuery] GetBoatsRequest request) =>
        Ok(await boatService.GetPageAsync(request, HttpContext.RequestAborted));

    /// <summary>
    /// Lấy thông tin chi tiết một tàu theo ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BoatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BoatResponse>> GetBoatById(Guid id) =>
        Ok(await boatService.GetByIdAsync(id, HttpContext.RequestAborted));

    /// <summary>
    /// Thêm tàu mới vào đội tàu (Yêu cầu quyền Admin)
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Guid>> CreateBoat([FromBody] CreateBoatRequest request)
    {
        var id = await boatService.CreateAsync(request, HttpContext.RequestAborted);
        return CreatedAtAction(nameof(GetBoatById), new { id }, id);
    }

    /// <summary>
    /// Cập nhật thông tin tàu (Yêu cầu quyền Admin)
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateBoat(Guid id, [FromBody] UpdateBoatRequest request)
    {
        await boatService.UpdateAsync(id, request, HttpContext.RequestAborted);
        return NoContent();
    }
}
