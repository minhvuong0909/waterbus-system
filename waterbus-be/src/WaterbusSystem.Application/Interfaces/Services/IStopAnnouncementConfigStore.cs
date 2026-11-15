using WaterbusSystem.Application.Features.SightseeingAudio;

namespace WaterbusSystem.Application.Interfaces.Services;

/// <summary>
/// Nguồn cấu hình thời gian thông báo bến đang chạy. Admin cập nhật tại runtime nên không dùng
/// IOptionsMonitor (chỉ đọc từ cấu hình). Mọi thành phần đọc cùng một bản cấu hình.
/// </summary>
public interface IStopAnnouncementConfigStore
{
    /// <summary>Cấu hình hiện hành.</summary>
    StopAnnouncementOptions Current { get; }

    /// <summary>Thay toàn bộ cấu hình bằng cấu hình đã kiểm tra ngân sách thời gian.</summary>
    void Replace(StopAnnouncementOptions options);
}
