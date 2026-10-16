using Microsoft.AspNetCore.Mvc;
using WaterbusSystem.Application.Features.Trips.Queries.SearchTrips;
using WaterbusSystem.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using WaterbusSystem.Application.Features.Trips.Queries.GetTripDetail;
using WaterbusSystem.Application.Features.Trips.Queries.GetTripSeatMap;

namespace WaterbusSystem.WebApi.Controllers;

/// <summary>
/// Tra cứu chuyến tàu và kiểm tra số lượng ghế trống
/// </summary>
public class TripsController : BaseApiController
{
    /// <summary>Chi tiết công khai của chuyến và các điểm dừng thực sự được phục vụ.</summary>
    [AllowAnonymous]
    [HttpGet("{tripId:guid}")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType(typeof(TripDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TripDetailDto>> GetDetail(Guid tripId) =>
        Ok(await Mediator.Send(new GetTripDetailQuery(tripId), HttpContext.RequestAborted));

    /// <summary>Sơ đồ ghế, giá phẳng và availability cho đoạn TripStopCall được chọn; không tạo hold.</summary>
    [AllowAnonymous]
    [HttpGet("{tripId:guid}/seats")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType(typeof(TripSeatMapDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TripSeatMapDto>> GetSeats(Guid tripId,
        [FromQuery] Guid boardingCallId, [FromQuery] Guid disembarkingCallId) =>
        Ok(await Mediator.Send(new GetTripSeatMapQuery(tripId, boardingCallId, disembarkingCallId), HttpContext.RequestAborted));

    /// <summary>
    /// Tìm kiếm danh sách chuyến tàu theo bộ lọc (Ga đi, Ga đến, Ngày khởi hành, Loại chuyến)
    /// </summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(List<TripDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TripDto>>> SearchTrips(
        [FromQuery] Guid? departureStationId,
        [FromQuery] Guid? arrivalStationId,
        [FromQuery] DateTime? departureDate,
        [FromQuery] TripType? tripType)
    {
        var result = await Mediator.Send(new SearchTripsQuery(departureStationId, arrivalStationId, departureDate, tripType));
        return Ok(result);
    }
}
