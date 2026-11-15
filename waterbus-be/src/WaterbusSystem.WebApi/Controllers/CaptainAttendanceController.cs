using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaterbusSystem.Application.DTOs.Requests.Captains;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.WebApi.Filters;

namespace WaterbusSystem.WebApi.Controllers;

[Authorize(Roles = "Admin,Captain")]
public sealed class CaptainAttendanceController(ICaptainOperationsService operations) : BaseApiController
{
    [HttpGet("/api/v1/trips/{tripId:guid}/captain-attendance")]
    [ProducesResponseType(typeof(WaterbusSystem.Application.DTOs.Responses.Captains.CaptainTripAttendanceResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid tripId, CancellationToken ct) => Ok(await operations.GetAttendanceAsync(tripId, ct));
}
