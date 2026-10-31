using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.Incidents;
using WaterbusSystem.Application.DTOs.Responses.Incidents;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.WebApi.Filters;

namespace WaterbusSystem.WebApi.Controllers;

/// <summary>
/// Sự cố vận hành (thời tiết, kỹ thuật, an toàn) cho dashboard điều phối của Admin.
/// Ghi nhận Incident KHÔNG tự đổi trạng thái chuyến (BR-INC-03).
/// </summary>
[Authorize(Policy = "AdminOnly")]
[ServiceFilter(typeof(ValidateRequestFilter))]
public class IncidentsController(
    IIncidentService incidentService,
    IWeatherStatusService weatherStatusService,
    IWeatherGuardService weatherGuardService) : BaseApiController
{

    [HttpGet]
    [ProducesResponseType(typeof(PaginatedList<IncidentSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedList<IncidentSummaryResponse>>> GetIncidents([FromQuery] GetIncidentsRequest request) =>
        Ok(await incidentService.GetPageAsync(request, HttpContext.RequestAborted));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(IncidentDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IncidentDetailResponse>> GetIncidentById(Guid id) =>
        Ok(await incidentService.GetByIdAsync(id, HttpContext.RequestAborted));

    [HttpGet("weather-now")]
    [ProducesResponseType(typeof(List<RouteWeatherResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<RouteWeatherResponse>>> GetCurrentWeather() =>
        Ok(await weatherStatusService.GetCurrentAsync(HttpContext.RequestAborted));

    [HttpPost("weather-check")]
    [ProducesResponseType(typeof(WeatherGuardResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<WeatherGuardResponse>> RunWeatherCheck() =>
        Ok(await weatherGuardService.RunAsync(HttpContext.RequestAborted));

    [HttpPost("{id:guid}/resolve")]
    [ProducesResponseType(typeof(ResolveIncidentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ResolveIncidentResponse>> ResolveIncident(Guid id) =>
        Ok(await incidentService.ResolveAsync(id, HttpContext.RequestAborted));
}
