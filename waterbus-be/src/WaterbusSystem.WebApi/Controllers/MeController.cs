using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.Accounts;
using WaterbusSystem.Application.DTOs.Responses.Accounts;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.WebApi.Filters;

namespace WaterbusSystem.WebApi.Controllers;

/// <summary>
/// Dữ liệu của Passenger đang đăng nhập. Phạm vi luôn là tài khoản trong JWT; Order/Ticket của người khác trả 404
/// (không phân biệt "không tồn tại" và "không phải của bạn"). QR/chi tiết Order dùng /checkouts/{orderId}/... với JWT chủ sở hữu.
/// </summary>
[Authorize(Roles = "Passenger")]
[Route("api/v1/me")]
public sealed class MeController(IOrderAccessService access) : BaseApiController
{
    [HttpGet("orders")]
    [ServiceFilter(typeof(ValidateRequestFilter))]
    public async Task<ActionResult<PaginatedList<MyOrderSummaryDto>>> Orders([FromQuery] GetMyOrdersRequest request) =>
        Ok(await access.GetMyOrdersAsync(AccountId(), request, HttpContext.RequestAborted));

    [HttpGet("tickets/{ticketId:guid}")]
    public async Task<ActionResult<TicketDetailDto>> Ticket(Guid ticketId) =>
        Ok(await access.GetMyTicketAsync(AccountId(), ticketId, HttpContext.RequestAborted));

    private Guid AccountId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id)
        ? id : throw new ForbiddenAccessException("Không xác định được tài khoản.");
}
