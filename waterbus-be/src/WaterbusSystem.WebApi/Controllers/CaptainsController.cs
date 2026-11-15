using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaterbusSystem.Application.DTOs.Requests.Captains;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Responses.Captains;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.WebApi.Filters;

namespace WaterbusSystem.WebApi.Controllers;

[Authorize(Roles = "Admin")]
[ServiceFilter(typeof(ValidateRequestFilter))]
public sealed class CaptainsController(ICaptainService captains, ICaptainOperationsService operations) : BaseApiController
{
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedList<CaptainResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] GetCaptainsRequest request, CancellationToken ct) => Ok(await captains.ListAsync(request, ct));
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CaptainResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(await captains.GetAsync(id, ct));
    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateCaptainRequest request, CancellationToken ct)
    {
        var id = await captains.CreateAsync(request, ct); return CreatedAtAction(nameof(Get), new { id }, id);
    }
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateCaptainRequest request, CancellationToken ct)
    { await captains.UpdateAsync(id, request, ct); return NoContent(); }
    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> Status(Guid id, ChangeCaptainStatusRequest request, CancellationToken ct)
    { await captains.ChangeStatusAsync(id, request, ct); return NoContent(); }
    [HttpPost("{id:guid}/invitation")]
    public async Task<IActionResult> Invite(Guid id, ResendCaptainInvitationRequest request, CancellationToken ct)
    { await captains.ResendInvitationAsync(id, request.Reason, ct); return NoContent(); }
    [HttpPut("/api/v1/boats/{boatId:guid}/captain")]
    public async Task<IActionResult> Assign(Guid boatId, AssignCaptainRequest request, CancellationToken ct)
    { await operations.AssignAsync(boatId, request, ct); return NoContent(); }
    [HttpPost("/api/v1/trips/{tripId:guid}/captain-attendance")]
    [ProducesResponseType(typeof(CaptainAttendanceResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Attend(Guid tripId, RecordCaptainAttendanceRequest request, CancellationToken ct) => Ok(await operations.RecordAttendanceAsync(tripId, request, ct));
    [HttpGet("audit")]
    [ProducesResponseType(typeof(PaginatedList<CaptainAuditEvent>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Audit([FromQuery] GetCaptainEventsRequest request, CancellationToken ct) => Ok(await operations.AuditAsync(request, ct));
    [HttpGet("alerts")]
    [ProducesResponseType(typeof(PaginatedList<CaptainAlert>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Alerts([FromQuery] GetCaptainEventsRequest request, CancellationToken ct) => Ok(await operations.AlertsAsync(request, ct));
    [HttpPost("alerts/{id:guid}/resolve")]
    public async Task<IActionResult> Resolve(Guid id, CancellationToken ct) { await operations.ResolveAlertAsync(id, ct); return NoContent(); }
}
