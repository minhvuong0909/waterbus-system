namespace WaterbusSystem.Application.Common.Interfaces;

/// <summary>
/// Phát vị trí tàu thời gian thực (BoatTrackingHub) tới radar Admin và người theo dõi từng tàu.
/// Application chỉ phụ thuộc interface này, không tham chiếu trực tiếp SignalR Hub.
/// </summary>
public interface IBoatTrackingPublisher
{
    Task PublishPositionAsync(BoatPositionMessage message, CancellationToken cancellationToken = default);
}

/// <summary>
/// Phát thay đổi trạng thái giữ/bán ghế theo đoạn (SeatSyncHub) tới mọi client đang xem sơ đồ ghế của Trip.
/// Chỉ gọi SAU KHI thay đổi đã commit thành công (Redis hold / DB), hub không quyết định nghiệp vụ.
/// </summary>
public interface ISeatEventPublisher
{
    Task SeatHeldAsync(SeatEventMessage message, CancellationToken cancellationToken = default);
    Task SeatReleasedAsync(SeatEventMessage message, CancellationToken cancellationToken = default);
    Task SeatSoldAsync(SeatEventMessage message, CancellationToken cancellationToken = default);
}

/// <summary>
/// Phát cảnh báo vận hành (thời tiết, sự cố) tới Admin đang theo dõi radar.
/// </summary>
public interface IOperationsAlertPublisher
{
    Task PublishWeatherAlertAsync(WeatherAlertMessage message, CancellationToken cancellationToken = default);
}

/// <summary>
/// Payload event <c>boatPosition</c>. Toạ độ theo WGS84.
/// </summary>
public sealed record BoatPositionMessage(
    Guid BoatId,
    Guid? TripId,
    decimal Lat,
    decimal Lng,
    decimal? SpeedKnots,
    DateTimeOffset ObservedAt)
{
    /// <summary>Phiên bản hợp đồng payload, tăng khi đổi cấu trúc để client nhận biết.</summary>
    public int Version => 1;
}

/// <summary>
/// Payload event <c>seatHeld</c> / <c>seatReleased</c> / <c>seatSold</c>.
/// Đoạn hành trình tính theo VisitOrder của TripStopCall (không dùng Station.OrderIndex).
/// </summary>
public sealed record SeatEventMessage(
    Guid TripId,
    Guid SeatId,
    int BoardingVisitOrder,
    int DisembarkingVisitOrder,
    string Status,
    DateTimeOffset? ExpiresAt)
{
    public int Version => 1;
}

/// <summary>
/// Payload event <c>weatherAlert</c>, tham chiếu Incident đã được ghi nhận.
/// </summary>
public sealed record WeatherAlertMessage(
    Guid IncidentId,
    string? Severity,
    Guid? RouteId,
    string Message,
    DateTimeOffset OccurredAt)
{
    public int Version => 1;
}
