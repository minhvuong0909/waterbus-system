using Microsoft.AspNetCore.Mvc;
using WaterbusSystem.Application.DTOs.Responses.Accounts;
using WaterbusSystem.Application.Interfaces.Services;

namespace WaterbusSystem.WebApi.Controllers;

/// <summary>
/// BR-ID-05/06: hành khách xem thông tin đi tàu qua Passenger TripAccess Link do Purchaser chia sẻ. Chỉ trả thông tin hành trình;
/// không có giá, thanh toán, thông tin Purchaser, QR hay quyền mở audio.
/// </summary>
public sealed class TripAccessController(IOrderAccessService access) : BaseApiController
{
    [HttpGet]
    public async Task<ActionResult<TripAccessDto>> Get([FromHeader(Name = "X-Trip-Access-Token")] string? tripAccessToken) =>
        Ok(await access.GetTripAccessAsync(tripAccessToken, HttpContext.RequestAborted));
}
