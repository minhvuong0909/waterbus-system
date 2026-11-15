using Microsoft.Extensions.Options;
using WaterbusSystem.Application.DTOs.Requests.SightseeingAudio;
using WaterbusSystem.Application.DTOs.Responses.SightseeingAudio;
using WaterbusSystem.Application.Interfaces.Services;

namespace WaterbusSystem.Application.Features.SightseeingAudio.Services;

/// <summary>
/// Quản lý cấu hình Stop Announcement in-memory cho các kịch bản mô phỏng.
/// </summary>
public sealed class StopAnnouncementAdminService(IOptionsMonitor<StopAnnouncementOptions> options) : IStopAnnouncementAdminService
{
    public StopAnnouncementConfigResponse GetConfiguration()
    {
        var current = options.CurrentValue;
        return new StopAnnouncementConfigResponse(
            current.SegmentDurationSeconds,
            current.DwellTimeSeconds,
            current.NearThreshold,
            current.UseSimulatedSegmentDuration);
    }

    public StopAnnouncementConfigResponse UpdateConfiguration(UpdateStopAnnouncementRequest request)
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

        return new StopAnnouncementConfigResponse(
            current.SegmentDurationSeconds,
            current.DwellTimeSeconds,
            current.NearThreshold,
            current.UseSimulatedSegmentDuration);
    }
}
