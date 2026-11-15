using WaterbusSystem.Application.DTOs.Requests.SightseeingAudio;
using WaterbusSystem.Application.DTOs.Responses.SightseeingAudio;

namespace WaterbusSystem.Application.Interfaces.Services;

/// <summary>
/// Dịch vụ quản trị cấu hình bộ mô phỏng thông báo bến.
/// Mọi cập nhật phải vượt qua ngân sách thời gian, nếu không sẽ ném ValidationException (HTTP 400).
/// </summary>
public interface IStopAnnouncementAdminService
{
    Task<StopAnnouncementConfigResponse> GetConfigurationAsync(CancellationToken cancellationToken = default);

    Task<StopAnnouncementConfigResponse> UpdateConfigurationAsync(
        UpdateStopAnnouncementRequest request, CancellationToken cancellationToken = default);
}
