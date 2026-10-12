using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using WaterbusSystem.WebApi.Realtime;

namespace WaterbusSystem.WebApi.Hubs;

/// <summary>
/// Hub đồng bộ sơ đồ ghế (/hubs/seat-sync). Cho phép Guest (ẩn danh) nhưng CHỈ ĐỌC:
/// không có method giữ/nhả ghế ở đây, việc đó luôn đi qua REST API.
/// Mỗi kết nối chỉ theo dõi tối đa <see cref="MaxTripsPerConnection"/> Trip để hạn chế lạm dụng kết nối ẩn danh.
/// </summary>
[AllowAnonymous]
public class SeatSyncHub : Hub
{
    /// <summary>Đủ cho round-trip (2 Trip) cộng vài tab đổi chuyến.</summary>
    public const int MaxTripsPerConnection = 4;
    private const string JoinedTripsKey = "joinedTrips";

    public Task JoinTrip(Guid tripId)
    {
        if (tripId == Guid.Empty)
        {
            throw new HubException("Mã chuyến không hợp lệ.");
        }

        var joined = JoinedTrips();
        if (!joined.Contains(tripId) && joined.Count >= MaxTripsPerConnection)
        {
            throw new HubException($"Mỗi kết nối chỉ theo dõi tối đa {MaxTripsPerConnection} chuyến.");
        }

        joined.Add(tripId);
        return Groups.AddToGroupAsync(Context.ConnectionId, RealtimeContract.Groups.Trip(tripId));
    }

    public Task LeaveTrip(Guid tripId)
    {
        JoinedTrips().Remove(tripId);
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, RealtimeContract.Groups.Trip(tripId));
    }

    private HashSet<Guid> JoinedTrips()
    {
        if (Context.Items.TryGetValue(JoinedTripsKey, out var value) && value is HashSet<Guid> set)
        {
            return set;
        }

        var created = new HashSet<Guid>();
        Context.Items[JoinedTripsKey] = created;
        return created;
    }
}
