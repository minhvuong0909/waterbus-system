using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaterbusSystem.Application.DTOs.Requests.SeatInventory;
using WaterbusSystem.Application.DTOs.Responses.SeatInventory;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.WebApi.Filters;

namespace WaterbusSystem.WebApi.Controllers;

/// <summary>Cấu hình ghế thật của tàu (chỉ Admin). Không xoá cứng ghế: dùng deactivate.</summary>
[ServiceFilter(typeof(ValidateRequestFilter))]
[Authorize(Policy = "AdminOnly")]
public class BoatSeatsController(IBoatSeatService service) : BaseApiController
{
    [HttpGet("~/api/v1/boats/{boatId:guid}/seats")]
    [ProducesResponseType(typeof(BoatSeatInventoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BoatSeatInventoryResponse>> Get(Guid boatId) =>
        Ok(await service.GetInventoryAsync(boatId, HttpContext.RequestAborted));

    [HttpPost("~/api/v1/boats/{boatId:guid}/seats")]
    [ProducesResponseType(typeof(BoatSeatInventoryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BoatSeatInventoryResponse>> Add(Guid boatId, [FromBody] AddBoatSeatsRequest request) =>
        CreatedAtAction(nameof(Get), new { boatId }, await service.AddSeatsAsync(boatId, request, HttpContext.RequestAborted));

    [HttpPut("~/api/v1/boats/{boatId:guid}/seats/{seatId:guid}")]
    [ProducesResponseType(typeof(BoatSeatInventoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BoatSeatInventoryResponse>> Update(Guid boatId, Guid seatId, [FromBody] UpdateBoatSeatRequest request) =>
        Ok(await service.UpdateSeatAsync(boatId, seatId, request, HttpContext.RequestAborted));

    [HttpPost("~/api/v1/boats/{boatId:guid}/seats/{seatId:guid}/deactivate")]
    [ProducesResponseType(typeof(BoatSeatInventoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BoatSeatInventoryResponse>> Deactivate(Guid boatId, Guid seatId) =>
        Ok(await service.DeactivateSeatAsync(boatId, seatId, HttpContext.RequestAborted));

    [HttpPost("~/api/v1/boats/{boatId:guid}/seats/{seatId:guid}/activate")]
    [ProducesResponseType(typeof(BoatSeatInventoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BoatSeatInventoryResponse>> Activate(Guid boatId, Guid seatId) =>
        Ok(await service.ActivateSeatAsync(boatId, seatId, HttpContext.RequestAborted));
}
