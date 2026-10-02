using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.Features.Boats.Commands.CreateBoat;
using WaterbusSystem.Application.Features.Boats.Commands.UpdateBoat;
using WaterbusSystem.Application.Features.Boats.Queries.GetBoatById;
using WaterbusSystem.Application.Features.Boats.Queries.GetBoats;

namespace WaterbusSystem.WebApi.Controllers;

/// <summary>
/// Quản lý danh mục tàu thủy chở khách (Boats)
/// </summary>
public class BoatsController : BaseApiController
{
    /// <summary>
    /// Lấy danh sách tàu có phân trang
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedList<BoatDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedList<BoatDto>>> GetBoats([FromQuery] GetBoatsQuery query)
    {
        var result = await Mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Lấy thông tin chi tiết một tàu theo ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BoatDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BoatDto>> GetBoatById(Guid id)
    {
        var result = await Mediator.Send(new GetBoatByIdQuery(id));
        return Ok(result);
    }

    /// <summary>
    /// Thêm tàu mới vào đội tàu (Yêu cầu quyền Admin)
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Guid>> CreateBoat([FromBody] CreateBoatCommand command)
    {
        var id = await Mediator.Send(command);
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
    public async Task<IActionResult> UpdateBoat(Guid id, [FromBody] UpdateBoatCommand command)
    {
        if (id != command.Id)
        {
            return BadRequest("ID trong URL không khớp với ID trong request body.");
        }

        await Mediator.Send(command);
        return NoContent();
    }
}
