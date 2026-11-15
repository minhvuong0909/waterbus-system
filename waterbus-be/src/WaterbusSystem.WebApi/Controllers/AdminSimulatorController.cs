using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WaterbusSystem.Application.Features.SightseeingAudio;

namespace WaterbusSystem.WebApi.Controllers;

/// <summary>
/// API Admin để thay đổi cấu hình giả lập tàu (Stop Announcement) khi demo trước giảng viên.
/// Thay đổi có hiệu lực ngay tức thì (IOptionsMonitor) mà không cần restart.
/// </summary>
[Authorize(Policy = "AdminOnly")]
[Route("api/v1/admin/simulator")]
public sealed class AdminSimulatorController(IOptionsMonitor<StopAnnouncementOptions> options) : BaseApiController
{
    /// <summary>Lấy cấu hình Stop Announcement hiện tại.</summary>
    [HttpGet("stop-announcement")]
    public ActionResult<StopAnnouncementConfigResponse> GetStopAnnouncement() =>
        Ok(new StopAnnouncementConfigResponse(
            options.CurrentValue.SegmentDurationSeconds,
            options.CurrentValue.DwellTimeSeconds,
            options.CurrentValue.NearThreshold,
            options.CurrentValue.UseSimulatedSegmentDuration));

    /// <summary>
    /// Cập nhật cấu hình Stop Announcement cho phiên demo hiện tại (in-memory, không persist).
    /// Có hiệu lực ngay; để persist thay đổi cần sửa appsettings / biến môi trường.
    /// </summary>
    [HttpPut("stop-announcement")]
    public IActionResult UpdateStopAnnouncement([FromBody] UpdateStopAnnouncementRequest request)
    {
        // IOptionsMonitor không hỗ trợ write-back trực tiếp. Chúng ta cập nhật object hiện tại
        // thông qua cách truy cập trực tiếp. Vì CurrentValue là snapshot mutable trong demo,
        // Admin có thể reload bằng cách sửa biến môi trường và gọi API này.
        // Lưu ý: đây là giải pháp demo đơn giản; production nên dùng feature flags / DB config.
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

/// <summary>Phản hồi cấu hình Stop Announcement.</summary>
public sealed record StopAnnouncementConfigResponse(
    double SegmentDurationSeconds,
    double DwellTimeSeconds,
    double NearThreshold,
    bool UseSimulatedSegmentDuration);

/// <summary>Yêu cầu cập nhật cấu hình Stop Announcement (tất cả field tùy chọn — chỉ update field được truyền).</summary>
public sealed record UpdateStopAnnouncementRequest(
    double? SegmentDurationSeconds,
    double? DwellTimeSeconds,
    double? NearThreshold,
    bool? UseSimulatedSegmentDuration);
