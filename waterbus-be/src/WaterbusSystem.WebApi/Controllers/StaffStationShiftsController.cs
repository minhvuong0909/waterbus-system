using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.ScannerDevices;
using WaterbusSystem.Application.DTOs.Responses.ScannerDevices;
using WaterbusSystem.Application.Interfaces.Services;

namespace WaterbusSystem.WebApi.Controllers;

[Authorize(Policy = "AdminOnly")]
public sealed class StaffStationShiftsController(IStaffStationShiftService shifts) : BaseApiController
{
    [HttpGet("~/api/v1/admin/staff-station-shifts")]
    public async Task<ActionResult<PaginatedList<StaffStationShiftResponse>>> List(
        [FromQuery, Range(1, 1000000)] int pageNumber = 1, [FromQuery, Range(1, 100)] int pageSize = 20) =>
        Ok(await shifts.ListAsync(pageNumber, pageSize, HttpContext.RequestAborted));

    [HttpPost("~/api/v1/admin/staff-station-shifts")]
    public async Task<ActionResult<Guid>> Create([FromBody] CreateStaffShiftRequest request) =>
        Ok(await shifts.CreateAsync(request, HttpContext.RequestAborted));

    [HttpPost("~/api/v1/admin/staff-station-shifts/{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid id)
    {
        await shifts.RevokeAsync(id, HttpContext.RequestAborted);
        return NoContent();
    }
}
