using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.Stations;
using WaterbusSystem.Application.DTOs.Responses.Stations;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.WebApi.Filters;

namespace WaterbusSystem.WebApi.Controllers;

/// <summary>
/// Quản lý danh mục bến buýt đường sông (Stations)
/// </summary>
[ServiceFilter(typeof(ValidateRequestFilter))]
public class StationsController(IStationService stationService) : BaseApiController
{
    /// <summary>
    /// Lấy danh sách toàn bộ bến tàu đang hoạt động theo thứ tự luồng sông
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<StationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<StationResponse>>> GetStations() =>
        Ok(await stationService.GetActiveStationsAsync(HttpContext.RequestAborted));

    /// <summary>
    /// Lấy danh sách bến tàu có phân trang
    /// </summary>
    [HttpGet("paged")]
    [ProducesResponseType(typeof(PaginatedList<StationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedList<StationResponse>>> GetStationsPaged([FromQuery] GetStationsPagedRequest request) =>
        Ok(await stationService.GetPageAsync(request, HttpContext.RequestAborted));

    /// <summary>
    /// Lấy thông tin chi tiết một bến tàu theo ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(StationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StationResponse>> GetStationById(Guid id) =>
        Ok(await stationService.GetByIdAsync(id, HttpContext.RequestAborted));

    /// <summary>
    /// Tạo bến tàu mới (Yêu cầu quyền Admin)
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Guid>> CreateStation([FromBody] CreateStationRequest request)
    {
        var id = await stationService.CreateAsync(request, HttpContext.RequestAborted);
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
    public async Task<IActionResult> UpdateStation(Guid id, [FromBody] UpdateStationRequest request)
    {
        await stationService.UpdateAsync(id, request, HttpContext.RequestAborted);
        return NoContent();
    }
}
