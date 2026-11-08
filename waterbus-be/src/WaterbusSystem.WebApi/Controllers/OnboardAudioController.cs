using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.DTOs.Requests.OnboardAudio;
using WaterbusSystem.Application.DTOs.Responses.OnboardAudio;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.WebApi.Filters;

namespace WaterbusSystem.WebApi.Controllers;

/// <summary>REST cho Onboard Player (đồng bộ sau reconnect, tải MP3 có kiểm soát, ACK/báo trạng thái). Chỉ Captain của Boat.</summary>
[Authorize(Policy = "CaptainOnly")]
[Route("api/v1/onboard-audio")]
public sealed class OnboardAudioController(IOnboardAudioService service) : BaseApiController
{
    [HttpGet("state")]
    public async Task<ActionResult<OnboardAudioStateResponse>> State() =>
        Ok(await service.GetStateAsync(CaptainId(), HttpContext.RequestAborted));

    /// <summary>MP3 của lệnh phát (cần JWT Captain đúng Boat và chuyến đang được phép phát). Hỗ trợ Range.</summary>
    [HttpGet("requests/{requestId:guid}/audio")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Audio(Guid requestId)
    {
        var audio = await service.GetAudioAsync(CaptainId(), requestId, HttpContext.RequestAborted);
        Response.Headers.CacheControl = "private, no-store";
        return File(audio.Data, audio.ContentType, enableRangeProcessing: true);
    }

    [HttpPost("requests/{requestId:guid}/accept")]
    public async Task<IActionResult> Accept(Guid requestId) =>
        Ok(new { requestId, pendingDispatchCompleted = await service.AcceptAsync(CaptainId(), requestId, HttpContext.RequestAborted) });

    [HttpPost("requests/{requestId:guid}/state")]
    [ServiceFilter(typeof(ValidateRequestFilter))]
    public async Task<ActionResult<PlaybackStateAck>> ReportState(Guid requestId, [FromBody] ReportPlaybackStateRequest request) =>
        Ok(await service.ReportStateAsync(CaptainId(), requestId, request, HttpContext.RequestAborted));

    private Guid CaptainId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id)
        ? id : throw new ForbiddenAccessException("Không xác định được tài khoản.");
}
