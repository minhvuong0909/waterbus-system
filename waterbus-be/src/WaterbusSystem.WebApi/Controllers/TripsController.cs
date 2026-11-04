using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaterbusSystem.Application.DTOs.Requests.Trips;
using WaterbusSystem.Application.DTOs.Responses.Trips;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.WebApi.Filters;

namespace WaterbusSystem.WebApi.Controllers;

/// <summary>
/// Tra cứu chuyến tàu và kiểm tra số lượng ghế trống
/// </summary>
[ServiceFilter(typeof(ValidateRequestFilter))]
public class TripsController(ITripService tripService) : BaseApiController
{
    /// <summary>Chi tiết công khai của chuyến và các điểm dừng thực sự được phục vụ.</summary>
    [AllowAnonymous]
    [HttpGet("{tripId:guid}")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType(typeof(TripDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TripDetailResponse>> GetDetail(Guid tripId) =>
        Ok(await tripService.GetDetailAsync(tripId, HttpContext.RequestAborted));

    /// <summary>Sơ đồ ghế, giá phẳng và availability cho đoạn TripStopCall được chọn; không tạo hold.</summary>
    [AllowAnonymous]
    [HttpGet("{tripId:guid}/seats")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType(typeof(TripSeatMapResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TripSeatMapResponse>> GetSeats(Guid tripId, [FromQuery] GetTripSeatMapRequest request) =>
        Ok(await tripService.GetSeatMapAsync(tripId, request, HttpContext.RequestAborted));

    /// <summary>
    /// Chuyến cho màn hình vận hành: Admin xem mọi chuyến, thuyền trưởng chỉ chuyến của tàu mình.
    /// Mặc định từ 12 giờ trước tới 7 ngày sau (tối đa 31 ngày, 200 chuyến). Có thể lọc theo trạng thái.
    /// </summary>
    [HttpGet("operations")]
    [Authorize(Roles = "Admin,Captain")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType(typeof(List<TripOperationItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<TripOperationItemResponse>>> GetOperations([FromQuery] GetTripOperationsRequest request) =>
        Ok(await tripService.GetOperationsAsync(request, HttpContext.RequestAborted));

    /// <summary>Nhật ký thao tác vận hành (ai đổi trạng thái chuyến nào, lúc nào, vì sao), mới nhất trước. Chỉ Admin.</summary>
    [HttpGet("operations/events")]
    [Authorize(Policy = "AdminOnly")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType(typeof(List<TripOperationEventResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TripOperationEventResponse>>> GetOperationEvents([FromQuery] GetTripOperationEventsRequest request) =>
        Ok(await tripService.GetOperationEventsAsync(request, HttpContext.RequestAborted));

    /// <summary>
    /// Đổi trạng thái chuyến. Thuyền trưởng chỉ OpenBoarding/Start/Arrive trên chuyến của tàu mình;
    /// Admin làm mọi thao tác (Suspend, Resume, Cancel, Terminate, Complete) và có thể gắn <c>incidentId</c>
    /// để ghi quyết định cho chuyến đó của sự cố. Suspend/Cancel/Terminate bắt buộc có lý do.
    /// </summary>
    [HttpPost("{tripId:guid}/status")]
    [Authorize(Roles = "Admin,Captain")]
    [ProducesResponseType(typeof(TripOperationResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TripOperationResultResponse>> ChangeStatus(Guid tripId, [FromBody] ChangeTripStatusRequest request) =>
        Ok(await tripService.ChangeStatusAsync(tripId, request, HttpContext.RequestAborted));

    /// <summary>
    /// Ghi nhận tàu đã đến/rời một điểm dừng trung gian của chuyến (không đổi trạng thái chuyến).
    /// Điểm dừng đầu/cuối tự cập nhật theo thao tác Start/Arrive ở trên; dùng endpoint này cho các
    /// điểm dừng ở giữa để đánh giá hoàn tiền (refund) đúng đoạn đã thực sự được phục vụ.
    /// </summary>
    [HttpPost("{tripId:guid}/stops/{tripStopCallId:guid}/progress")]
    [Authorize(Roles = "Admin,Captain")]
    [ProducesResponseType(typeof(StopProgressResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StopProgressResultResponse>> RecordStopProgress(
        Guid tripId, Guid tripStopCallId, [FromBody] RecordStopProgressRequest request) =>
        Ok(await tripService.RecordStopProgressAsync(tripId, tripStopCallId, request, HttpContext.RequestAborted));

    /// <summary>
    /// Thuyền trưởng báo khẩn cấp (SOS) cho chuyến của tàu mình: tạo sự cố mức cao, báo Admin ngay.
    /// Không đổi trạng thái chuyến; Admin quyết định.
    /// </summary>
    [HttpPost("{tripId:guid}/sos")]
    [Authorize(Policy = "CaptainOnly")]
    [ProducesResponseType(typeof(SosReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SosReportResponse>> ReportSos(Guid tripId, [FromBody] ReportTripSosRequest request) =>
        Ok(await tripService.ReportSosAsync(tripId, request, HttpContext.RequestAborted));

    /// <summary>
    /// Tìm kiếm danh sách chuyến tàu theo bộ lọc (Ga đi, Ga đến, Ngày khởi hành, Loại chuyến)
    /// </summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(List<TripSearchResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TripSearchResponse>>> SearchTrips(
        [FromQuery] Guid? departureStationId,
        [FromQuery] Guid? arrivalStationId,
        [FromQuery] DateTime? departureDate,
        [FromQuery] TripType? tripType) =>
        Ok(await tripService.SearchAsync(new SearchTripsRequest(departureStationId, arrivalStationId, departureDate, tripType), HttpContext.RequestAborted));
}
