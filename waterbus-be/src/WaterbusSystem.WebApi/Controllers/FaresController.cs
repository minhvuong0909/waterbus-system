using Microsoft.AspNetCore.Mvc;
using WaterbusSystem.Application.DTOs.Responses.Fares;
using WaterbusSystem.Application.Interfaces.Services;

namespace WaterbusSystem.WebApi.Controllers;

/// <summary>
/// Bảng giá vé đang hiệu lực (công khai, chỉ đọc). Quản lý bảng giá chưa có API.
/// </summary>
public class FaresController : BaseApiController
{
    private readonly IFareService _fareService;

    public FaresController(IFareService fareService)
    {
        _fareService = fareService;
    }

    /// <summary>
    /// Lấy các mức giá đang hiệu lực theo loại chuyến (Commuter/Sightseeing) và hạng ghế.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<FareResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<FareResponse>>> GetFares()
    {
        return Ok(await _fareService.GetActiveFaresAsync(HttpContext.RequestAborted));
    }
}
