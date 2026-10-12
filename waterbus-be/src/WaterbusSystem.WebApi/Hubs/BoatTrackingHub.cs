using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using WaterbusSystem.WebApi.Realtime;

namespace WaterbusSystem.WebApi.Hubs;

/// <summary>
/// Hub theo dõi vị trí tàu (/hubs/boat-tracking). Bắt buộc đăng nhập.
/// Client chỉ join/leave group; dữ liệu được server đẩy xuống qua IBoatTrackingPublisher / IOperationsAlertPublisher.
/// </summary>
[Authorize]
public class BoatTrackingHub : Hub
{
    /// <summary>Radar toàn đội tàu + cảnh báo vận hành: chỉ Admin.</summary>
    [Authorize(Policy = "AdminOnly")]
    public Task JoinFleet() =>
        Groups.AddToGroupAsync(Context.ConnectionId, RealtimeContract.Groups.Fleet);

    public Task LeaveFleet() =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, RealtimeContract.Groups.Fleet);

    /// <summary>Theo dõi một tàu cụ thể (vd Captain Portal hiển thị tàu của mình).</summary>
    public Task JoinBoat(Guid boatId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, RealtimeContract.Groups.Boat(boatId));

    public Task LeaveBoat(Guid boatId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, RealtimeContract.Groups.Boat(boatId));
}
