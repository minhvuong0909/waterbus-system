using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WaterbusSystem.Application.Features.SightseeingAudio;

namespace WaterbusSystem.WebApi.Controllers;

/// <summary>
/// Quản lý cấu hình mô phỏng chuyển động tàu và thông báo trạm bến.
/// </summary>
[Authorize(Policy = "AdminOnly")]
[Route("api/v1/admin/simulator")]
public sealed class AdminSimulatorController(IOptionsMonitor<StopAnnouncementOptions> options) : BaseApiController
{
    /// <summary>Lấy cấu hình hiện tại của bộ mô phỏng.</summary>
    [HttpGet("stop-announcement")]
    public ActionResult<StopAnnouncementConfigResponse> GetStopAnnouncement() =>
        Ok(new StopAnnouncementConfigResponse(
            options.CurrentValue.SegmentDurationSeconds,
            options.CurrentValue.DwellTimeSeconds,
            options.CurrentValue.NearThreshold,
            options.CurrentValue.UseSimulatedSegmentDuration));

    /// <summary>Cập nhật cấu hình mô phỏng hiện tại.</summary>
    [HttpPut("stop-announcement")]
    public IActionResult UpdateStopAnnouncement([FromBody] UpdateStopAnnouncementRequest request)
    {
        var current = options.CurrentValue;
        if (request.SegmentDurationSeconds.HasValue)
            current.SegmentDurationSeconds = request.SegmentDurationSeconds.Value;
        if (request.DwellTimeSeconds.HasValue)
            current.DwellTimeSeconds = request.DwellTimeSeconds.Value;
        if (request.NearThreshold.HasValue)
            current.NearThreshold = Math.Clamp(request.NearThreshold.Value, 0.1, 0.99);
        if (request.UseSimulatedSegmentDuration.HasValue)
            current.UseSimulatedSegmentDuration = request.UseSimulatedSegmentDuration.Value;

        return Ok(new StopAnnouncementConfigResponse(
            current.SegmentDurationSeconds,
            current.DwellTimeSeconds,
            current.NearThreshold,
            current.UseSimulatedSegmentDuration));
    }
}

public sealed record StopAnnouncementConfigResponse(
    double SegmentDurationSeconds,
    double DwellTimeSeconds,
    double NearThreshold,
    bool UseSimulatedSegmentDuration);

public sealed record UpdateStopAnnouncementRequest(
    double? SegmentDurationSeconds,
    double? DwellTimeSeconds,
    double? NearThreshold,
    bool? UseSimulatedSegmentDuration);
