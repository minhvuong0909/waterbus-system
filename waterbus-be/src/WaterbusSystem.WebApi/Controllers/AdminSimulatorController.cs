using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaterbusSystem.Application.DTOs.Requests.SightseeingAudio;
using WaterbusSystem.Application.DTOs.Responses.SightseeingAudio;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.WebApi.Filters;

namespace WaterbusSystem.WebApi.Controllers;

/// <summary>
/// Quản lý cấu hình mô phỏng chuyển động tàu và thông báo trạm bến.
/// </summary>
[Authorize(Policy = "AdminOnly")]
[Route("api/v1/admin/simulator")]
public sealed class AdminSimulatorController(IStopAnnouncementAdminService service) : BaseApiController
{
    /// <summary>Lấy cấu hình hiện tại của bộ mô phỏng.</summary>
    [HttpGet("stop-announcement")]
    public ActionResult<StopAnnouncementConfigResponse> GetStopAnnouncement() =>
        Ok(service.GetConfiguration());

    /// <summary>Cập nhật cấu hình mô phỏng hiện tại.</summary>
    [HttpPut("stop-announcement")]
    [ServiceFilter(typeof(ValidateRequestFilter))]
    public ActionResult<StopAnnouncementConfigResponse> UpdateStopAnnouncement([FromBody] UpdateStopAnnouncementRequest request) =>
        Ok(service.UpdateConfiguration(request));
}
