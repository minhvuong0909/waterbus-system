using Microsoft.AspNetCore.Mvc;
using WaterbusSystem.Application.Features.Stations.Queries.GetStations;

namespace WaterbusSystem.WebApi.Controllers;

/// <summary>
/// Quản lý danh mục bến buýt đường sông
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
}
