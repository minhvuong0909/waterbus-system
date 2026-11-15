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
public sealed class MeController(IOrderAccessService access, IProfileService profile, IOrderLinkingService linking) : BaseApiController
{
    [HttpGet("orders")]
    [ServiceFilter(typeof(ValidateRequestFilter))]
    public async Task<ActionResult<PaginatedList<MyOrderSummaryDto>>> Orders([FromQuery] GetMyOrdersRequest request) =>
        Ok(await access.GetMyOrdersAsync(AccountId(), request, HttpContext.RequestAborted));

    [HttpGet("tickets/{ticketId:guid}")]
    public async Task<ActionResult<TicketDetailDto>> Ticket(Guid ticketId) =>
        Ok(await access.GetMyTicketAsync(AccountId(), ticketId, HttpContext.RequestAborted));

    /// <summary>Hồ sơ của chính tài khoản đang đăng nhập.</summary>
    [HttpGet("profile")]
    public async Task<ActionResult<ProfileResponse>> GetProfile() =>
        Ok(await profile.GetAsync(AccountId(), HttpContext.RequestAborted));

    [HttpPut("profile")]
    [ServiceFilter(typeof(ValidateRequestFilter))]
    public async Task<ActionResult<ProfileResponse>> UpdateProfile([FromBody] UpdateProfileRequest request) =>
        Ok(await profile.UpdateAsync(AccountId(), request, HttpContext.RequestAborted));

    /// <summary>Số đơn mua bằng email đã xác minh của tài khoản và chưa gắn tài khoản nào. 403 nếu email chưa xác minh.</summary>
    [HttpGet("orders/linkable")]
    public async Task<ActionResult<LinkableOrdersResponse>> LinkableOrders() =>
        Ok(await linking.CountLinkableAsync(AccountId(), HttpContext.RequestAborted));

    /// <summary>Gắn các đơn đó vào tài khoản (tối đa 200 mỗi lần; còn đơn thì gọi lại). Idempotent.</summary>
    [HttpPost("orders/link-by-email")]
    public async Task<ActionResult<LinkOrdersResponse>> LinkOrdersByEmail() =>
        Ok(await linking.LinkByVerifiedEmailAsync(AccountId(), HttpContext.RequestAborted));

    private Guid AccountId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id)
        ? id : throw new ForbiddenAccessException("Không xác định được tài khoản.");
}
