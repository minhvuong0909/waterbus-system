using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.StaffAccounts;
using WaterbusSystem.Application.DTOs.Responses.StaffAccounts;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.WebApi.Filters;

namespace WaterbusSystem.WebApi.Controllers;

/// <summary>Quản lý tài khoản Staff (soát vé) — chỉ Admin. Route: /api/v1/staff-accounts.</summary>
[Authorize(Policy = "AdminOnly")]
[Route("api/v1/staff-accounts")]
[ServiceFilter(typeof(ValidateRequestFilter))]
public sealed class StaffAccountsController(IStaffAccountService staff) : BaseApiController
{
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedList<StaffAccountResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] GetStaffAccountsRequest request, CancellationToken ct) =>
        Ok(await staff.ListAsync(request, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(StaffAccountResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(await staff.GetAsync(id, ct));

    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateStaffAccountRequest request, CancellationToken ct)
    {
        var id = await staff.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id }, id);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateStaffAccountRequest request, CancellationToken ct)
    {
        await staff.UpdateAsync(id, request, ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> Status(Guid id, ChangeStaffAccountStatusRequest request, CancellationToken ct)
    {
        await staff.ChangeStatusAsync(id, request, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/invitation")]
    public async Task<IActionResult> Invite(Guid id, ResendStaffInvitationRequest request, CancellationToken ct)
    {
        await staff.ResendInvitationAsync(id, request.Reason, ct);
        return NoContent();
    }
}
