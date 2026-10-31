using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.Tracking;
using WaterbusSystem.Application.DTOs.Responses.Tracking;
using WaterbusSystem.Application.Features.Tracking;
using WaterbusSystem.Application.Interfaces.Services;

namespace WaterbusSystem.WebApi.Controllers;

/// <summary>
/// Theo dõi vị trí tàu. Realtime đi qua SignalR /hubs/boat-tracking; các API này dùng để
/// lấy snapshot khi mở trang / sau khi reconnect, xem lịch sử, và gửi vị trí khi không dùng được WebSocket.
/// </summary>
public class TrackingController(IBoatTrackingService tracking) : BaseApiController
{
    /// <summary>
    /// Vị trí mới nhất của các tàu đang hoạt động (radar Admin)
    /// </summary>
    [HttpGet("boats")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(List<BoatLatestPositionResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<BoatLatestPositionResponse>>> GetLatestPositions() =>
        Ok(await tracking.GetLatestPositionsAsync(HttpContext.RequestAborted));

    /// <summary>
    /// Lịch sử vị trí đã lưu của một tàu trong khoảng thời gian (tối đa 7 ngày), có phân trang
    /// </summary>
    [HttpGet("boats/{boatId:guid}/history")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(PaginatedList<BoatPositionHistoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaginatedList<BoatPositionHistoryResponse>>> GetHistory(
        Guid boatId,
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 100) =>
        Ok(await tracking.GetPositionHistoryAsync(
            new GetBoatPositionHistoryRequest(boatId, from, to, pageNumber, pageSize), HttpContext.RequestAborted));

    /// <summary>
    /// Captain gửi vị trí GPS của tàu mình (dự phòng khi không dùng được SignalR)
    /// </summary>
    [HttpPost("boats/{boatId:guid}/positions")]
    [Authorize(Policy = "CaptainOnly")]
    [ProducesResponseType(typeof(RecordBoatPositionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RecordBoatPositionResponse>> SendPosition(Guid boatId, [FromBody] SendBoatPositionRequest request)
    {
        var captainId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : (Guid?)null;
        return Ok(await tracking.RecordPositionAsync(
            new RecordBoatPositionRequest(
                boatId, request.Latitude, request.Longitude, request.SpeedKnots, request.ObservedAt,
                BoatPositionSources.Gps, captainId),
            HttpContext.RequestAborted));
    }
}
