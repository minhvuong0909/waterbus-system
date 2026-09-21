using Microsoft.AspNetCore.Mvc;
using WaterbusSystem.Application.Features.Trips.Queries.SearchTrips;
using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.WebApi.Controllers;

/// <summary>
/// Tra cứu chuyến tàu và kiểm tra số lượng ghế trống
/// </summary>
public class TripsController : BaseApiController
{
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
