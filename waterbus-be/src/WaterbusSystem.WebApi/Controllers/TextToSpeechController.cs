using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaterbusSystem.Application.Features.SightseeingAudio.Commands.SynthesizeAudio;
using WaterbusSystem.Application.Features.SightseeingAudio.Queries.GetAudioAsset;

namespace WaterbusSystem.WebApi.Controllers;

/// <summary>
/// API sinh giọng nói tiếng Việt từ văn bản bằng Azure AI Speech (Neural Voice).
/// Chỉ Admin được phép gọi để tạo tài nguyên âm thanh (BR-CFG-01).
/// </summary>
[Authorize(Policy = "AdminOnly")]
public class TextToSpeechController : BaseApiController
{
    /// <summary>
    /// Nhận kịch bản văn bản tiếng Việt → gọi Azure TTS sinh file .mp3 → lưu trữ kho lưu trữ → trả về đường dẫn tệp MP3 và metadata.
    /// Hoàn toàn độc lập, không yêu cầu POI và không lưu vào CSDL.
    /// </summary>
    /// <param name="command">Văn bản kịch bản tiếng Việt (Text hoặc ScriptText) và tùy chọn giọng đọc.</param>
    [HttpPost("generate")]
    [ProducesResponseType(typeof(SynthesizeAudioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<SynthesizeAudioDto>> Generate([FromBody] SynthesizeAudioCommand command)
    {
        var result = await Mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Trả tệp MP3 đã sinh theo AssetKey để Admin nghe thử (Blob private, không có URL công khai).
    /// </summary>
    /// <param name="assetKey">AssetKey trả về từ <c>generate</c>, ví dụ <c>audio-assets/tts/20261023/abc.mp3</c>.</param>
    [HttpGet("audio")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK, "audio/mpeg")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Audio([FromQuery] string assetKey)
    {
        var audio = await Mediator.Send(new GetAudioAssetQuery(assetKey));
        return File(audio.Data, audio.ContentType);
    }
}
