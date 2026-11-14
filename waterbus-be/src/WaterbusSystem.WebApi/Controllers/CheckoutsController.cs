using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.DTOs.Responses.Accounts;
using WaterbusSystem.Application.DTOs.Requests.Bookings;
using WaterbusSystem.Application.DTOs.Responses.Bookings;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.WebApi.Filters;

namespace WaterbusSystem.WebApi.Controllers;

[ServiceFilter(typeof(ValidateRequestFilter))]
public sealed class CheckoutsController(ICheckoutService checkoutService, ICheckoutReadService reads, IBookingQrService qr,
    IOrderAccessService orderAccess) : BaseApiController
{
    /// <summary>
    /// BR-ID-04: Passenger đăng nhập liên kết Order mua khi còn là Guest. Bắt buộc có Manage Order token hợp lệ của
    /// chính Order này; không bao giờ gán theo email trùng. Gọi lặp bởi cùng tài khoản là idempotent; tài khoản khác → 409.
    /// </summary>
    [HttpPost("{orderId:guid}/claim")]
    [Authorize(Roles = "Passenger")]
    [ProducesResponseType(typeof(ClaimOrderResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Claim(Guid orderId, [FromHeader(Name = "X-Guest-Token")] string? accessToken) =>
        Ok(await orderAccess.ClaimAsync(orderId, CurrentAccountId(), accessToken, HttpContext.RequestAborted));

    /// <summary>Đổi Manage Order link: token cũ bị thu hồi ngay (chống replay). Chủ sở hữu đăng nhập có thể đổi không cần token cũ.</summary>
    [HttpPost("{orderId:guid}/access/rotate")]
    public async Task<ActionResult<AccessTokenResponse>> RotateAccess(Guid orderId, [FromHeader(Name = "X-Guest-Token")] string? accessToken) =>
        Ok(await orderAccess.RotateAsync(orderId, accessToken, HttpContext.RequestAborted));

    /// <summary>Thu hồi mọi Manage Order và TripAccess link của Order.</summary>
    [HttpPost("{orderId:guid}/access/revoke")]
    public async Task<ActionResult<RevokeAccessResponse>> RevokeAccess(Guid orderId, [FromHeader(Name = "X-Guest-Token")] string? accessToken) =>
        Ok(await orderAccess.RevokeAsync(orderId, accessToken, HttpContext.RequestAborted));

    /// <summary>BR-ID-05: Purchaser tạo Passenger TripAccess Link cho một Booking đã thanh toán để chia sẻ.</summary>
    [HttpPost("{orderId:guid}/bookings/{bookingId:guid}/trip-access")]
    public async Task<ActionResult<AccessTokenResponse>> IssueTripAccess(Guid orderId, Guid bookingId,
        [FromHeader(Name = "X-Guest-Token")] string? accessToken) =>
        Ok(await orderAccess.IssueTripAccessAsync(orderId, bookingId, accessToken, HttpContext.RequestAborted));

    [HttpGet("{orderId:guid}/tickets/{ticketId:guid}")]
    public async Task<ActionResult<TicketDetailDto>> GetTicket(Guid orderId, Guid ticketId,
        [FromHeader(Name = "X-Guest-Token")] string? accessToken) =>
        Ok(await orderAccess.GetTicketAsync(orderId, ticketId, accessToken, HttpContext.RequestAborted));

    private Guid CurrentAccountId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id)
        ? id : throw new ForbiddenAccessException("Không xác định được tài khoản.");


    [HttpPost]
    [EnableRateLimiting("BookingPolicy")]
    [ProducesResponseType(
        typeof(CheckoutResponse),
        StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        [FromBody] CreateCheckoutRequest request)
    {
        var result = await checkoutService.CreateAsync(
            request,
            HttpContext.RequestAborted);

        return CreatedAtAction(
            nameof(Get),
            new { orderId = result.OrderId },
            result);
    }


    [HttpGet("{orderId:guid}")]
    public async Task<IActionResult> Get(Guid orderId, [FromHeader(Name = "X-Guest-Token")] string? accessToken) =>
        Ok(await reads.GetAsync(orderId, accessToken, HttpContext.RequestAborted));


    [HttpPost("holds")]
    [EnableRateLimiting("BookingPolicy")]
    [ProducesResponseType(typeof(CheckoutResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Hold(
        [FromBody] CreateCheckoutHoldRequest request)
    {
        var result = await checkoutService.HoldAsync(request, HttpContext.RequestAborted);

        return CreatedAtAction(
            nameof(Get),
            new { orderId = result.OrderId },
            result);
    }


    [HttpPost("{orderId:guid}/submit")]
    public async Task<IActionResult> Submit(
        Guid orderId,
        [FromBody] SubmitCheckoutRequest request,
        [FromHeader(Name = "X-Guest-Token")] string? accessToken)
    {
        var result = await checkoutService.SubmitAsync(
            orderId,
            request,
            accessToken,
            HttpContext.RequestAborted);

        return Ok(result);
    }

    [HttpDelete("{orderId:guid}")]
    public async Task<IActionResult> Cancel(
        Guid orderId,
        [FromHeader(Name = "X-Guest-Token")] string? accessToken)
    {
        var result = await checkoutService.CancelAsync(
            orderId,
            accessToken,
            HttpContext.RequestAborted);

        return Ok(result);
    }


    [HttpPut("{orderId:guid}/bookings/{bookingId:guid}/seats")]
    public async Task<IActionResult> UpdateSeats(
        Guid orderId,
        Guid bookingId,
        [FromBody] UpdateCheckoutSeatsRequest request,
        [FromHeader(Name = "X-Guest-Token")] string? accessToken)
    {
        var quotedTotal = await checkoutService.UpdateSeatsAsync(
            orderId,
            bookingId,
            request,
            accessToken,
            HttpContext.RequestAborted);

        return Ok(new { quotedTotal });
    }

    [HttpGet("{orderId:guid}/bookings/{bookingId:guid}/qr")]
    public async Task<IActionResult> Qr(Guid orderId, Guid bookingId, [FromHeader(Name = "X-Guest-Token")] string? accessToken) =>
        Ok(await qr.GetAsync(orderId, bookingId, accessToken, HttpContext.RequestAborted));

    [HttpGet("qr-public-key")]
    public IActionResult PublicKey() => Ok(qr.GetPublicKey());
}
