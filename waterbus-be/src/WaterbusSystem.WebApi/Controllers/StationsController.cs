using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.Features.Stations.Commands.CreateStation;
using WaterbusSystem.Application.Features.Stations.Commands.UpdateStation;
using WaterbusSystem.Application.Features.Stations.Queries.GetStationById;
using WaterbusSystem.Application.Features.Stations.Queries.GetStations;

namespace WaterbusSystem.WebApi.Controllers;

/// <summary>
/// Quản lý danh mục bến buýt đường sông (Stations)
/// </summary>
public class StationsController : BaseApiController
{
    /// <summary>
    /// Lấy danh sách toàn bộ bến tàu đang hoạt động theo thứ tự luồng sông
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<StationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<StationDto>>> GetStations()
    {
        var result = await Mediator.Send(new GetStationsQuery());
        return Ok(result);
    }

    /// <summary>
    /// Lấy danh sách bến tàu có phân trang
    /// </summary>
    [HttpGet("paged")]
    [ProducesResponseType(typeof(PaginatedList<StationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedList<StationDto>>> GetStationsPaged([FromQuery] GetStationsPagedQuery query)
    {
        var result = await Mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Lấy thông tin chi tiết một bến tàu theo ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(StationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StationDto>> GetStationById(Guid id)
    {
        var result = await Mediator.Send(new GetStationByIdQuery(id));
        return Ok(result);
    }

    /// <summary>
    /// Tạo bến tàu mới (Yêu cầu quyền Admin)
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Guid>> CreateStation([FromBody] CreateStationCommand command)
    {
        var id = await Mediator.Send(command);
        return CreatedAtAction(nameof(GetStationById), new { id }, id);
    }

    /// <summary>
    /// Cập nhật thông tin bến tàu (Yêu cầu quyền Admin)
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStation(Guid id, [FromBody] UpdateStationCommand command)
    {
        if (id != command.Id)
        {
            return BadRequest("ID trong URL không khớp với ID trong request body.");
        }

        await Mediator.Send(command);
        return NoContent();
    }
}
