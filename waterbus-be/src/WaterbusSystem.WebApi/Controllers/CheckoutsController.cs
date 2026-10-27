using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WaterbusSystem.Application.DTOs.Requests.Bookings;
using WaterbusSystem.Application.DTOs.Responses.Bookings;
using WaterbusSystem.Application.Interfaces.Services;

namespace WaterbusSystem.WebApi.Controllers;

public sealed class CheckoutsController(ICheckoutService checkoutService, ICheckoutReadService reads, IBookingQrService qr) : BaseApiController
{

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
