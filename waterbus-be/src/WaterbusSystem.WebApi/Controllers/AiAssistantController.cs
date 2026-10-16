using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WaterbusSystem.Application.Features.AiBooking.Models;
using WaterbusSystem.Application.Features.AiBooking.Queries.AskAiBooking;

namespace WaterbusSystem.WebApi.Controllers;

/// <summary>Trợ lý tìm chuyến và dẫn tới luồng đặt vé, không tạo Booking/Payment.</summary>
public sealed class AiAssistantController : BaseApiController
{
    /// <summary>Nhận câu hỏi khách hàng và trả Trip Card có cấu trúc từ tool tra cứu.</summary>
    [HttpPost("chat")]
    [AllowAnonymous]
    [EnableRateLimiting("AiAssistantPolicy")]
    [ProducesResponseType(typeof(AiBookingChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<AiBookingChatResponse>> Chat([FromBody] AiBookingChatRequest request)
    {
        return Ok(await Mediator.Send(new AskAiBookingQuery(request)));
    }
}
