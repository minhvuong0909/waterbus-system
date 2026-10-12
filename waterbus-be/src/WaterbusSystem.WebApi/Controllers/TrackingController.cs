using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.Features.Tracking.Commands.RecordBoatPosition;
using WaterbusSystem.Application.Features.Tracking.Queries.GetBoatPositionHistory;
using WaterbusSystem.Application.Features.Tracking.Queries.GetLatestBoatPositions;

namespace WaterbusSystem.WebApi.Controllers;

/// <summary>
/// Theo dõi vị trí tàu. Realtime đi qua SignalR /hubs/boat-tracking; các API này dùng để
/// lấy snapshot khi mở trang / sau khi reconnect, xem lịch sử, và gửi vị trí khi không dùng được WebSocket.
/// </summary>
public class TrackingController : BaseApiController
{
    /// <summary>
    /// Vị trí mới nhất của các tàu đang hoạt động (radar Admin)
    /// </summary>
    [HttpGet("boats")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(List<BoatLatestPositionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<BoatLatestPositionDto>>> GetLatestPositions()
    {
        var result = await Mediator.Send(new GetLatestBoatPositionsQuery());
        return Ok(result);
    }

    /// <summary>
    /// Lịch sử vị trí đã lưu của một tàu trong khoảng thời gian (tối đa 7 ngày), có phân trang
    /// </summary>
    [HttpGet("boats/{boatId:guid}/history")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(PaginatedList<BoatPositionHistoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaginatedList<BoatPositionHistoryDto>>> GetHistory(
        Guid boatId,
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 100)
    {
        var result = await Mediator.Send(new GetBoatPositionHistoryQuery(boatId, from, to, pageNumber, pageSize));
        return Ok(result);
    }

    /// <summary>
    /// Captain gửi vị trí GPS của tàu mình (dự phòng khi không dùng được SignalR)
    /// </summary>
    [HttpPost("boats/{boatId:guid}/positions")]
    [Authorize(Policy = "CaptainOnly")]
    [ProducesResponseType(typeof(RecordBoatPositionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RecordBoatPositionResult>> SendPosition(Guid boatId, [FromBody] SendBoatPositionRequest request)
    {
        var captainId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : (Guid?)null;
        var result = await Mediator.Send(new RecordBoatPositionCommand(
            boatId, request.Latitude, request.Longitude, request.SpeedKnots, request.ObservedAt,
            BoatPositionSources.Gps, captainId));
        return Ok(result);
    }
}

public record SendBoatPositionRequest(decimal Latitude, decimal Longitude, decimal? SpeedKnots, DateTimeOffset ObservedAt);
