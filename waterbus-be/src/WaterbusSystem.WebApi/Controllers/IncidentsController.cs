using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.Features.Incidents.Commands.ResolveIncident;
using WaterbusSystem.Application.Features.Incidents.Commands.RunWeatherGuard;
using WaterbusSystem.Application.Features.Incidents.Queries.GetIncidentById;
using WaterbusSystem.Application.Features.Incidents.Queries.GetIncidents;

namespace WaterbusSystem.WebApi.Controllers;

/// <summary>
/// Sự cố vận hành (thời tiết, kỹ thuật, an toàn) cho dashboard điều phối của Admin.
/// Ghi nhận Incident KHÔNG tự đổi trạng thái chuyến (BR-INC-03).
/// </summary>
[Authorize(Policy = "AdminOnly")]
public class IncidentsController : BaseApiController
{
    /// <summary>
    /// Danh sách sự cố (mới nhất trước), lọc theo trạng thái (Open/Resolved/Closed) và loại (Weather/...)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedList<IncidentSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedList<IncidentSummaryDto>>> GetIncidents([FromQuery] GetIncidentsQuery query)
    {
        var result = await Mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Chi tiết sự cố kèm các chuyến bị ảnh hưởng và quyết định của Admin cho từng chuyến
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(IncidentDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IncidentDetailDto>> GetIncidentById(Guid id)
    {
        var result = await Mediator.Send(new GetIncidentByIdQuery(id));
        return Ok(result);
    }

    /// <summary>
    /// Chạy Weather Guard ngay (không chờ lịch định kỳ), trả về số Incident tạo/cập nhật
    /// </summary>
    [HttpPost("weather-check")]
    [ProducesResponseType(typeof(WeatherGuardResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<WeatherGuardResult>> RunWeatherCheck()
    {
        var result = await Mediator.Send(new RunWeatherGuardCommand());
        return Ok(result);
    }

    /// <summary>
    /// Đóng sự cố đang mở. Không đổi trạng thái chuyến; 409 nếu sự cố đã đóng.
    /// </summary>
    [HttpPost("{id:guid}/resolve")]
    [ProducesResponseType(typeof(ResolvedIncidentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ResolvedIncidentDto>> ResolveIncident(Guid id)
    {
        var result = await Mediator.Send(new ResolveIncidentCommand(id));
        return Ok(result);
    }
}
