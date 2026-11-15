using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaterbusSystem.Application.DTOs.Requests.SightseeingAudio;
using WaterbusSystem.Application.DTOs.Responses.SightseeingAudio;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.WebApi.Filters;

namespace WaterbusSystem.WebApi.Controllers;

/// <summary>
/// Quản lý cấu hình mô phỏng chuyển động tàu và thông báo trạm bến.
/// Cấu hình chỉ được lưu khi thoả ngân sách thời gian, nếu không API trả 400 kèm lý do.
/// </summary>
[Authorize(Policy = "AdminOnly")]
[Route("api/v1/admin/simulator")]
public sealed class AdminSimulatorController(IStopAnnouncementAdminService service) : BaseApiController
{
    /// <summary>Lấy cấu hình hiện tại của bộ mô phỏng kèm ngân sách thời gian đã dựng.</summary>
    [HttpGet("stop-announcement")]
    public async Task<ActionResult<StopAnnouncementConfigResponse>> GetStopAnnouncement(
        CancellationToken cancellationToken) =>
        Ok(await service.GetConfigurationAsync(cancellationToken));

    /// <summary>Cập nhật cấu hình mô phỏng hiện tại.</summary>
    [HttpPut("stop-announcement")]
    [ServiceFilter(typeof(ValidateRequestFilter))]
    public async Task<ActionResult<StopAnnouncementConfigResponse>> UpdateStopAnnouncement(
        [FromBody] UpdateStopAnnouncementRequest request, CancellationToken cancellationToken) =>
        Ok(await service.UpdateConfigurationAsync(request, cancellationToken));
}
