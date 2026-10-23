using Microsoft.AspNetCore.Mvc;
using WaterbusSystem.Application.Features.Fares.Queries.GetFares;

namespace WaterbusSystem.WebApi.Controllers;

/// <summary>
/// Bảng giá vé đang hiệu lực (công khai, chỉ đọc). Quản lý bảng giá chưa có API.
/// </summary>
public class FaresController : BaseApiController
{
    /// <summary>
    /// Lấy các mức giá đang hiệu lực theo loại chuyến (Commuter/Sightseeing) và hạng ghế.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<FareDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<FareDto>>> GetFares()
    {
        return Ok(await Mediator.Send(new GetFaresQuery()));
    }
}
