using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaterbusSystem.Application.DTOs.Requests.SightseeingAudio;
using WaterbusSystem.Application.DTOs.Responses.SightseeingAudio;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.WebApi.Filters;

namespace WaterbusSystem.WebApi.Controllers;

/// <summary>Liên kết MP3 Azure đã duyệt với POI để geofence có thể chọn audio thật.</summary>
[Authorize(Policy = "AdminOnly")]
[ServiceFilter(typeof(ValidateRequestFilter))]
public sealed class AudioGuidesController(IAudioGuideService audioGuides) : BaseApiController
{
    /// <summary>Liệt kê POI từ DB cùng trạng thái AudioGuide tiếng Việt.</summary>
    [HttpGet("pois")]
    [ProducesResponseType(typeof(AudioGuidePoisPageResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AudioGuidePoisPageResponse>> GetPois([FromQuery] GetAudioGuidePoisRequest request)
    {
        return Ok(await audioGuides.GetPoisAsync(request, HttpContext.RequestAborted));
    }

    /// <summary>
    /// Admin duyệt và publish MP3 đã tạo qua TextToSpeech/generate cho POI.
    /// Không tạo POI hoặc chấp nhận URI ngoài container Blob được cấu hình.
    /// </summary>
    [HttpPost("publish")]
    [ProducesResponseType(typeof(PublishedAudioGuideResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublishedAudioGuideResponse>> Publish(
        [FromBody] PublishAudioGuideRequest request)
    {
        return Ok(await audioGuides.PublishAsync(request, HttpContext.RequestAborted));
    }
}
