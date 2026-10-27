using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaterbusSystem.Application.Features.SightseeingAudio.Commands.PublishAudioGuide;
using WaterbusSystem.Application.Features.SightseeingAudio.Queries.GetAudioGuidePois;

namespace WaterbusSystem.WebApi.Controllers;

/// <summary>Liên kết MP3 Azure đã duyệt với POI để geofence có thể chọn audio thật.</summary>
[Authorize(Policy = "AdminOnly")]
public sealed class AudioGuidesController : BaseApiController
{
    /// <summary>Liệt kê POI từ DB cùng trạng thái AudioGuide tiếng Việt.</summary>
    [HttpGet("pois")]
    [ProducesResponseType(typeof(AudioGuidePoisPageDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AudioGuidePoisPageDto>> GetPois([FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        return Ok(await Mediator.Send(new GetAudioGuidePoisQuery(page, pageSize)));
    }

    /// <summary>
    /// Admin duyệt và publish MP3 đã tạo qua TextToSpeech/generate cho POI.
    /// Không tạo POI hoặc chấp nhận URI ngoài container Blob được cấu hình.
    /// </summary>
    [HttpPost("publish")]
    [ProducesResponseType(typeof(PublishedAudioGuideDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublishedAudioGuideDto>> Publish(
        [FromBody] PublishAudioGuideCommand command)
    {
        return Ok(await Mediator.Send(command));
    }
}
