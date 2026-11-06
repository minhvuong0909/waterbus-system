using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaterbusSystem.Application.DTOs.Requests.TripPlanning;
using WaterbusSystem.Application.DTOs.Responses.TripPlanning;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.WebApi.Filters;

namespace WaterbusSystem.WebApi.Controllers;

/// <summary>Sinh Trip từ Schedule và gán/đổi tàu (chỉ Admin). Đổi trạng thái Trip vẫn qua TripsController.</summary>
[ServiceFilter(typeof(ValidateRequestFilter))]
[Authorize(Policy = "AdminOnly")]
public class AdminTripsController(ITripPlanningService service) : BaseApiController
{
    /// <summary>Sinh Trip (chưa có tàu) cho các ngày đã chọn; idempotent: ngày đã có Trip được bỏ qua.</summary>
    [HttpPost("~/api/v1/admin/trips/generate")]
    [ProducesResponseType(typeof(GenerateTripsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GenerateTripsResponse>> Generate([FromBody] GenerateTripsRequest request) =>
        Ok(await service.GenerateFromScheduleAsync(request, HttpContext.RequestAborted));

    /// <summary>Gán hoặc đổi tàu cho Trip Scheduled; ghế đã bán được ánh xạ sang ghế cùng mã + hạng của tàu mới.</summary>
    [HttpPut("~/api/v1/admin/trips/{tripId:guid}/boat")]
    [ProducesResponseType(typeof(AssignTripBoatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AssignTripBoatResponse>> AssignBoat(Guid tripId, [FromBody] AssignTripBoatRequest request) =>
        Ok(await service.AssignBoatAsync(tripId, request, HttpContext.RequestAborted));
}
