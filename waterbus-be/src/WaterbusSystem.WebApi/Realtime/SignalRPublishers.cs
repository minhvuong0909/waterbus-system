using Microsoft.AspNetCore.SignalR;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.WebApi.Hubs;

namespace WaterbusSystem.WebApi.Realtime;

/// <summary>
/// Cài đặt IBoatTrackingPublisher bằng SignalR. Đặt ở WebApi vì Infrastructure không tham chiếu ASP.NET Core.
/// </summary>
public class SignalRBoatTrackingPublisher : IBoatTrackingPublisher
{
    private readonly IHubContext<BoatTrackingHub> _hubContext;

    public SignalRBoatTrackingPublisher(IHubContext<BoatTrackingHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task PublishPositionAsync(BoatPositionMessage message, CancellationToken cancellationToken = default) =>
        _hubContext.Clients
            .Groups(RealtimeContract.Groups.Fleet, RealtimeContract.Groups.Boat(message.BoatId))
            .SendAsync(RealtimeContract.Events.BoatPosition, message, cancellationToken);
}

/// <summary>
/// Cài đặt ISeatEventPublisher bằng SignalR: gửi tới group trip:{tripId}.
/// </summary>
public class SignalRSeatEventPublisher : ISeatEventPublisher
{
    private readonly IHubContext<SeatSyncHub> _hubContext;

    public SignalRSeatEventPublisher(IHubContext<SeatSyncHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task SeatHeldAsync(SeatEventMessage message, CancellationToken cancellationToken = default) =>
        SendAsync(RealtimeContract.Events.SeatHeld, message, cancellationToken);

    public Task SeatReleasedAsync(SeatEventMessage message, CancellationToken cancellationToken = default) =>
        SendAsync(RealtimeContract.Events.SeatReleased, message, cancellationToken);

    public Task SeatSoldAsync(SeatEventMessage message, CancellationToken cancellationToken = default) =>
        SendAsync(RealtimeContract.Events.SeatSold, message, cancellationToken);

    private Task SendAsync(string eventName, SeatEventMessage message, CancellationToken cancellationToken) =>
        _hubContext.Clients
            .Group(RealtimeContract.Groups.Trip(message.TripId))
            .SendAsync(eventName, message, cancellationToken);
}

/// <summary>
/// Cài đặt IOperationsAlertPublisher bằng SignalR. Cảnh báo và sự cố tới group fleet (Admin) trên BoatTrackingHub;
/// trạng thái chuyến tới Admin, thuyền trưởng của tàu (group boat) và khách đang xem sơ đồ ghế của chuyến (group trip).
/// </summary>
public class SignalROperationsAlertPublisher : IOperationsAlertPublisher
{
    private readonly IHubContext<BoatTrackingHub> _trackingHub;
    private readonly IHubContext<SeatSyncHub> _seatHub;

    public SignalROperationsAlertPublisher(IHubContext<BoatTrackingHub> trackingHub, IHubContext<SeatSyncHub> seatHub)
    {
        _trackingHub = trackingHub;
        _seatHub = seatHub;
    }

    public Task PublishWeatherAlertAsync(WeatherAlertMessage message, CancellationToken cancellationToken = default) =>
        _trackingHub.Clients
            .Group(RealtimeContract.Groups.Fleet)
            .SendAsync(RealtimeContract.Events.WeatherAlert, message, cancellationToken);

    public Task PublishIncidentRaisedAsync(IncidentRaisedMessage message, CancellationToken cancellationToken = default) =>
        _trackingHub.Clients
            .Group(RealtimeContract.Groups.Fleet)
            .SendAsync(RealtimeContract.Events.IncidentRaised, message, cancellationToken);

    public async Task PublishTripStatusChangedAsync(TripStatusChangedMessage message, CancellationToken cancellationToken = default)
    {
        var trackingGroups = message.BoatId is { } boatId
            ? new[] { RealtimeContract.Groups.Fleet, RealtimeContract.Groups.Boat(boatId) }
            : new[] { RealtimeContract.Groups.Fleet };

        await _trackingHub.Clients.Groups(trackingGroups)
            .SendAsync(RealtimeContract.Events.TripStatusChanged, message, cancellationToken);
        await _seatHub.Clients.Group(RealtimeContract.Groups.Trip(message.TripId))
            .SendAsync(RealtimeContract.Events.TripStatusChanged, message, cancellationToken);
    }
}
