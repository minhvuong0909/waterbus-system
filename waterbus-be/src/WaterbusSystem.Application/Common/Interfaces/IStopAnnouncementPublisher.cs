using WaterbusSystem.Application.Common.Models;

namespace WaterbusSystem.Application.Common.Interfaces;

/// <summary>
/// Phát thông báo bến (rời bến / gần tới / đã tới) qua SignalR tới tất cả client đang theo dõi chuyến.
/// Áp dụng cho MỌI loại chuyến (Commuter lẫn Sightseeing). Application chỉ phụ thuộp interface này,
/// không tham chiếu Hub hay SignalR trực tiếp.
/// </summary>
public interface IStopAnnouncementPublisher
{
    /// <summary>
    /// Phát thông báo "rời bến [stationName]" khi tàu bắt đầu xuất bến đi chặng tiếp theo.
    /// Với chuyến Sightseeing: kèm <paramref name="narration"/> để client phát tiếp thuyết minh địa danh.
    /// </summary>
    Task PublishDepartingAsync(
        Guid tripId,
        Guid boatId,
        Guid stationId,
        string stationName,
        DateTimeOffset at,
        string? tripType = null,
        string? nextStationName = null,
        StopNarrationPayload? narration = null,
        CancellationToken cancellationToken = default);

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
    /// Phát thông báo "đã tới bến [stationName]" khi progress = 1.0 (tàu cập bến / đỗ đón trả khách).
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
/// type = "departing" | "near" | "arrived".
/// </summary>
public sealed record StopAnnouncementMessage(
    Guid TripId,
    Guid BoatId,
    Guid StationId,
    string StationName,
    string Type,
    double SegmentProgress,
    DateTimeOffset At,
    string? TripType = null,
    string? NextStationName = null,
    StopNarrationPayload? Narration = null)
{
    /// <summary>Phiên bản hợp đồng payload, tăng khi đổi cấu trúc.</summary>
    public int Version => 2;
}
