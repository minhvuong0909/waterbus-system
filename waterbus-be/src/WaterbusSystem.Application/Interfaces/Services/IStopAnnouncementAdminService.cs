using WaterbusSystem.Application.DTOs.Requests.SightseeingAudio;
using WaterbusSystem.Application.DTOs.Responses.SightseeingAudio;

namespace WaterbusSystem.Application.Interfaces.Services;

/// <summary>
/// Dịch vụ quản trị cấu hình bộ mô phỏng thông báo bến.
/// </summary>
public interface IStopAnnouncementAdminService
{
    StopAnnouncementConfigResponse GetConfiguration();
    StopAnnouncementConfigResponse UpdateConfiguration(UpdateStopAnnouncementRequest request);
}
