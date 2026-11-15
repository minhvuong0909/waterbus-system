namespace WaterbusSystem.Application.Common.Interfaces;

/// <summary>
/// Phát thông báo bến (gần tới / đã tới) qua SignalR tới tất cả client đang theo dõi chuyến.
/// Áp dụng cho MỌI loại chuyến (Commuter lẫn Sightseeing). Application chỉ phụ thuộc interface này,
/// không tham chiếu Hub hay SignalR trực tiếp.
/// </summary>
public interface IStopAnnouncementPublisher
{
    /// <summary>
    /// Phát thông báo "gần tới bến [stationName]" khi progress đạt NearThreshold.
    /// </summary>
    Task PublishNearStopAsync(
        Guid tripId,
        Guid boatId,
        Guid stationId,
        string stationName,
        double segmentProgress,
        DateTimeOffset at,
        string? tripType = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Phát thông báo "đã tới bến [stationName]" khi progress = 1.0 (đầu đoạn tiếp theo).
    /// </summary>
    Task PublishArrivedAsync(
        Guid tripId,
        Guid boatId,
        Guid stationId,
        string stationName,
        double segmentProgress,
        DateTimeOffset at,
        string? tripType = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Payload event <c>stopAnnouncement</c>. Phát tới group trip:{tripId} và boat:{boatId}.
/// type = "near" hoặc "arrived".
/// </summary>
public sealed record StopAnnouncementMessage(
    Guid TripId,
    Guid BoatId,
    Guid StationId,
    string StationName,
    string Type,
    double SegmentProgress,
    DateTimeOffset At,
    string? TripType = null)
{
    /// <summary>Phiên bản hợp đồng payload, tăng khi đổi cấu trúc.</summary>
    public int Version => 1;
}
