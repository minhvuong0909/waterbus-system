using WaterbusSystem.Application.Features.SightseeingAudio;
using WaterbusSystem.Application.Interfaces.Services;

namespace WaterbusSystem.Infrastructure.Services.SightseeingAudio;

/// <summary>
/// Giữ cấu hình thời gian thông báo bến đang chạy. Đọc/ghi bằng khóa để nhiều request
/// Admin và simulator không đọc được hai phiên bản khác nhau cùng lúc.
/// </summary>
public sealed class StopAnnouncementConfigStore : IStopAnnouncementConfigStore
{
    private readonly object _gate = new();
    private StopAnnouncementOptions _current;

    public StopAnnouncementConfigStore(StopAnnouncementOptions initial)
    {
        _current = initial.Clone();
    }

    public StopAnnouncementOptions Current
    {
        get
        {
            lock (_gate)
            {
                return _current.Clone();
            }
        }
    }

    public void Replace(StopAnnouncementOptions options)
    {
        lock (_gate)
        {
            _current = options.Clone();
        }
    }
}
