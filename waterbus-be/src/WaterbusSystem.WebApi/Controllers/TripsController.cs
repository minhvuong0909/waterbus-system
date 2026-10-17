using Microsoft.AspNetCore.Mvc;
using WaterbusSystem.Application.Features.Trips.Queries.SearchTrips;
using WaterbusSystem.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using WaterbusSystem.Application.Features.Trips.Queries.GetTripDetail;
using WaterbusSystem.Application.Features.Trips.Queries.GetTripSeatMap;
using WaterbusSystem.Application.Features.Trips.Commands.ChangeTripStatus;
using WaterbusSystem.Application.Features.Trips.Commands.ReportTripSos;
using WaterbusSystem.Application.Features.Trips.Queries.GetTripOperationEvents;
using WaterbusSystem.Application.Features.Trips.Queries.GetTripOperations;

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
    /// Chuyến cho màn hình vận hành: Admin xem mọi chuyến, thuyền trưởng chỉ chuyến của tàu mình.
    /// Mặc định từ 12 giờ trước tới 7 ngày sau (tối đa 31 ngày, 200 chuyến). Có thể lọc theo trạng thái.
    /// </summary>
    [HttpGet("operations")]
    [Authorize(Roles = "Admin,Captain")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType(typeof(List<TripOperationItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<TripOperationItemDto>>> GetOperations(
        [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, [FromQuery] TripStatus[]? status) =>
        Ok(await Mediator.Send(new GetTripOperationsQuery(from, to, status), HttpContext.RequestAborted));

    /// <summary>Nhật ký thao tác vận hành (ai đổi trạng thái chuyến nào, lúc nào, vì sao), mới nhất trước. Chỉ Admin.</summary>
    [HttpGet("operations/events")]
    [Authorize(Policy = "AdminOnly")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType(typeof(List<TripOperationEventDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TripOperationEventDto>>> GetOperationEvents(
        [FromQuery] Guid? tripId, [FromQuery] int take = 50) =>
        Ok(await Mediator.Send(new GetTripOperationEventsQuery(tripId, take), HttpContext.RequestAborted));

    /// <summary>
    /// Đổi trạng thái chuyến. Thuyền trưởng chỉ OpenBoarding/Start/Arrive trên chuyến của tàu mình;
    /// Admin làm mọi thao tác (Suspend, Resume, Cancel, Terminate, Complete) và có thể gắn <c>incidentId</c>
    /// để ghi quyết định cho chuyến đó của sự cố. Suspend/Cancel/Terminate bắt buộc có lý do.
    /// </summary>
    [HttpPost("{tripId:guid}/status")]
    [Authorize(Roles = "Admin,Captain")]
    [ProducesResponseType(typeof(TripOperationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TripOperationResultDto>> ChangeStatus(Guid tripId, [FromBody] ChangeTripStatusRequest request) =>
        Ok(await Mediator.Send(
            new ChangeTripStatusCommand(tripId, request.Operation, request.Reason, request.IncidentId),
            HttpContext.RequestAborted));

    /// <summary>
    /// Thuyền trưởng báo khẩn cấp (SOS) cho chuyến của tàu mình: tạo sự cố mức cao, báo Admin ngay.
    /// Không đổi trạng thái chuyến; Admin quyết định.
    /// </summary>
    [HttpPost("{tripId:guid}/sos")]
    [Authorize(Policy = "CaptainOnly")]
    [ProducesResponseType(typeof(SosReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SosReportDto>> ReportSos(Guid tripId, [FromBody] ReportTripSosRequest request) =>
        Ok(await Mediator.Send(new ReportTripSosCommand(tripId, request.Note), HttpContext.RequestAborted));

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

/// <summary>Thân yêu cầu đổi trạng thái chuyến; <c>operation</c> là tên thao tác (vd "Suspend") hoặc số.</summary>
public record ChangeTripStatusRequest(TripOperation Operation, string? Reason, Guid? IncidentId);

public record ReportTripSosRequest(string? Note);
