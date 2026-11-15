namespace WaterbusSystem.Application.Interfaces.Services;

/// <summary>
/// Chống phát lặp thông báo bến theo (chuyến, chặng, loại thông báo) và theo địa danh đã thuyết minh.
/// Dùng chung cho lệnh vận hành (thuyền trưởng bấm rời bến) và bộ mô phỏng hành trình,
/// nên cùng một sự kiện chỉ phát đúng một lần.
/// </summary>
public interface IStopAnnouncementEmissionGuard
{
    bool TryClaim(Guid tripId, int segmentIndex, string announcementType);

    bool TryClaimNarration(Guid tripId, Guid poiId);
}
