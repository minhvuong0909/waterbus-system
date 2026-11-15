using Microsoft.AspNetCore.SignalR;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.WebApi.Hubs;

namespace WaterbusSystem.WebApi.Realtime;

/// <summary>
/// Cài đặt IStopAnnouncementPublisher bằng SignalR.
/// Phát event <c>stopAnnouncement</c> tới group <c>trip:{tripId}</c> và <c>boat:{boatId}</c>
/// trên BoatTrackingHub (hub public, cả Passenger lẫn Admin đều nhận được).
/// </summary>
public sealed class SignalRStopAnnouncementPublisher : IStopAnnouncementPublisher
{
    private readonly IHubContext<BoatTrackingHub> _hub;

    public SignalRStopAnnouncementPublisher(IHubContext<BoatTrackingHub> hub)
    {
        _hub = hub;
    }

    public Task PublishNearStopAsync(
        Guid tripId, Guid boatId, Guid stationId, string stationName,
        double segmentProgress, DateTimeOffset at, string? tripType = null,
        CancellationToken cancellationToken = default)
        => SendAsync(tripId, boatId, stationId, stationName, "near", segmentProgress, at, tripType, cancellationToken);

    public Task PublishArrivedAsync(
        Guid tripId, Guid boatId, Guid stationId, string stationName,
        double segmentProgress, DateTimeOffset at, string? tripType = null,
        CancellationToken cancellationToken = default)
        => SendAsync(tripId, boatId, stationId, stationName, "arrived", segmentProgress, at, tripType, cancellationToken);

    private Task SendAsync(
        Guid tripId, Guid boatId, Guid stationId, string stationName,
        string type, double segmentProgress, DateTimeOffset at, string? tripType,
        CancellationToken cancellationToken)
    {
        var message = new StopAnnouncementMessage(tripId, boatId, stationId, stationName, type, segmentProgress, at, tripType);
        return _hub.Clients
            .Groups(RealtimeContract.Groups.Trip(tripId), RealtimeContract.Groups.Boat(boatId))
            .SendAsync(RealtimeContract.Events.StopAnnouncement, message, cancellationToken);
    }
}
