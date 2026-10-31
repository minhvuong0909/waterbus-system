using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.DTOs.Requests.Tracking;
using WaterbusSystem.Application.Features.Tracking;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.WebApi.Realtime;

namespace WaterbusSystem.WebApi.Hubs;

/// <summary>
/// Hub theo dõi vị trí tàu (/hubs/boat-tracking). Bắt buộc đăng nhập.
/// Client join/leave group để nhận dữ liệu; Captain gửi GPS qua SendPosition (logic nằm ở IBoatTrackingService).
/// </summary>
[Authorize]
public class BoatTrackingHub(IBoatTrackingService tracking) : Hub
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

    /// <summary>
    /// Captain gửi vị trí GPS của tàu mình (từ Captain Portal, Geolocation trình duyệt).
    /// Trả về true nếu vị trí được nhận, false nếu bị bỏ qua vì cũ hơn vị trí đã có.
    /// </summary>
    [Authorize(Policy = "CaptainOnly")]
    public async Task<bool> SendPosition(Guid boatId, decimal latitude, decimal longitude, decimal? speedKnots, DateTimeOffset observedAt)
    {
        var captainId = Guid.TryParse(Context.UserIdentifier, out var id) ? id : (Guid?)null;

        try
        {
            var result = await tracking.RecordPositionAsync(new RecordBoatPositionRequest(
                boatId, latitude, longitude, speedKnots, observedAt, BoatPositionSources.Gps, captainId));
            return result.Accepted;
        }
        // SignalR chỉ chuyển thông điệp của HubException về client; lỗi khác bị ẩn thành lỗi chung
        catch (ValidationException ex)
        {
            throw new HubException(string.Join(" ", ex.Errors.SelectMany(e => e.Value)));
        }
        catch (Exception ex) when (ex is ForbiddenAccessException or NotFoundException)
        {
            throw new HubException(ex.Message);
        }
    }
}
