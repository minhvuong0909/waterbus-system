using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaterbusSystem.Application.Features.SightseeingAudio.Commands.SynthesizeAudio;

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
}
