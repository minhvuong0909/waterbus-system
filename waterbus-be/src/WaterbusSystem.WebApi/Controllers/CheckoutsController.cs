using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WaterbusSystem.Application.Features.Bookings.Commands.CreateCheckout;
using WaterbusSystem.Application.Features.Bookings.Commands.CancelCheckout;
using WaterbusSystem.Application.Features.Bookings.Commands.UpdateCheckoutSeats;
using WaterbusSystem.Application.Features.Bookings.Queries.GetCheckout;
using WaterbusSystem.Application.Features.Bookings.Queries.GetBookingQr;
using WaterbusSystem.Application.Features.Bookings.Queries.GetBookingQrPublicKey;
using WaterbusSystem.Application.Features.Bookings.Commands.CreateCheckoutHold;
using WaterbusSystem.Application.Features.Bookings.Commands.SubmitCheckout;

namespace WaterbusSystem.WebApi.Controllers;

public sealed class CheckoutsController : BaseApiController
{
    [HttpPost]
    [EnableRateLimiting("BookingPolicy")]
    [ProducesResponseType(typeof(CheckoutResponseDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateCheckoutCommand command)
    {
        var result = await Mediator.Send(command, HttpContext.RequestAborted);
        return CreatedAtAction(nameof(Get), new { orderId = result.OrderId }, result);
    }

    [HttpGet("{orderId:guid}")]
    public async Task<IActionResult> Get(Guid orderId, [FromHeader(Name = "X-Guest-Token")] string? accessToken) =>
        Ok(await Mediator.Send(new GetCheckoutQuery(orderId, accessToken), HttpContext.RequestAborted));

    [HttpPost("holds")]
    [EnableRateLimiting("BookingPolicy")]
    public async Task<IActionResult> Hold(CreateCheckoutHoldCommand command)
    {
        var result = await Mediator.Send(command, HttpContext.RequestAborted);
        return CreatedAtAction(nameof(Get), new { orderId = result.OrderId }, result);
    }

    [HttpPost("{orderId:guid}/submit")]
    public async Task<IActionResult> Submit(Guid orderId, SubmitCheckoutCommand command,
        [FromHeader(Name = "X-Guest-Token")] string? accessToken) =>
        Ok(await Mediator.Send(command with { OrderId = orderId, AccessToken = accessToken }, HttpContext.RequestAborted));

    [HttpDelete("{orderId:guid}")]
    public async Task<IActionResult> Cancel(Guid orderId, [FromHeader(Name = "X-Guest-Token")] string? accessToken) =>
        Ok(await Mediator.Send(new CancelCheckoutCommand(orderId, accessToken), HttpContext.RequestAborted));

    [HttpPut("{orderId:guid}/bookings/{bookingId:guid}/seats")]
    public async Task<IActionResult> UpdateSeats(Guid orderId, Guid bookingId, UpdateCheckoutSeatsCommand command,
        [FromHeader(Name = "X-Guest-Token")] string? accessToken) =>
        Ok(new { quotedTotal = await Mediator.Send(command with { OrderId = orderId, BookingId = bookingId, AccessToken = accessToken }, HttpContext.RequestAborted) });

    [HttpGet("{orderId:guid}/bookings/{bookingId:guid}/qr")]
    public async Task<IActionResult> Qr(Guid orderId, Guid bookingId, [FromHeader(Name = "X-Guest-Token")] string? accessToken) =>
        Ok(await Mediator.Send(new GetBookingQrQuery(orderId, bookingId, accessToken), HttpContext.RequestAborted));

    [HttpGet("qr-public-key")]
    public async Task<IActionResult> PublicKey() => Ok(await Mediator.Send(new GetBookingQrPublicKeyQuery(), HttpContext.RequestAborted));
}
