using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using WaterbusSystem.WebApi.Realtime;

namespace WaterbusSystem.WebApi.Hubs;

/// <summary>
/// Hub đồng bộ sơ đồ ghế (/hubs/seat-sync). Cho phép Guest (ẩn danh) nhưng CHỈ ĐỌC:
/// không có method giữ/nhả ghế ở đây, việc đó luôn đi qua REST API.
/// </summary>
[AllowAnonymous]
public class SeatSyncHub : Hub
{
    public Task JoinTrip(Guid tripId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, RealtimeContract.Groups.Trip(tripId));

    public Task LeaveTrip(Guid tripId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, RealtimeContract.Groups.Trip(tripId));
}
