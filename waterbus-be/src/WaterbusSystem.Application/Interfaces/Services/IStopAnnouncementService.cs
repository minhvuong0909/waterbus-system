namespace WaterbusSystem.Application.Interfaces.Services;

/// <summary>
/// Phát thông báo bến theo hành trình chuyến.
/// - Mọi loại chuyến (Commuter và Sightseeing) đều có thông báo rời bến / gần tới bến / đã tới bến (BR-EXP-01).
/// - Chuyến Sightseeing nhận kèm bài thuyết minh địa danh khi rời bến.
/// </summary>
public interface IStopAnnouncementService
{
    /// <summary>Phát thông báo rời bến (và thuyết minh địa danh nếu là chuyến Sightseeing).</summary>
    Task<bool> AnnounceDepartingAsync(Guid tripId, int fromVisitOrder, CancellationToken cancellationToken = default);

    /// <summary>Phát thông báo gần tới bến kế tiếp.</summary>
    Task<bool> AnnounceNearStopAsync(Guid tripId, int toVisitOrder, double segmentProgress, CancellationToken cancellationToken = default);

    /// <summary>Phát thông báo đã tới bến.</summary>
    Task<bool> AnnounceArrivedAsync(Guid tripId, int toVisitOrder, double segmentProgress, CancellationToken cancellationToken = default);
}
