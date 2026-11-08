using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.DTOs.Requests.OnboardAudio;
using WaterbusSystem.Application.DTOs.Responses.OnboardAudio;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.WebApi.Realtime;

namespace WaterbusSystem.WebApi.Hubs;

/// <summary>
/// Hub cho Onboard Player (/hubs/onboard-audio). Chỉ Captain; Boat được suy ra từ tài khoản (Boat.CaptainUserId), client KHÔNG
/// được chọn Boat, nên không thể nghe audio của tàu khác. Passenger/Guest/TripAccess không có quyền vào hub này (BR-ID-06).
/// Hub không quyết định nghiệp vụ; sau reconnect client phải gọi GetState (hoặc REST /onboard-audio/state) để đồng bộ.
/// </summary>
[Authorize(Policy = "CaptainOnly")]
public class OnboardAudioHub(IOnboardAudioService service, OnboardPlayerRegistry registry) : Hub
{
    public override async Task OnConnectedAsync()
    {
        try
        {
            var boatId = await service.ResolveBoatAsync(CaptainId());
            await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeContract.Groups.OnboardAudio(boatId));
            await Clients.Caller.SendAsync(RealtimeContract.Events.AudioState, await service.GetStateAsync(CaptainId()));
            // Đăng ký làm player hoạt động SAU CÙNG: nếu các bước trên ném lỗi thì không để lại kết nối "ma"
            // (SignalR không gọi OnDisconnectedAsync khi OnConnectedAsync ném).
            registry.Register(boatId, Context.ConnectionId, CaptainId());
        }
        catch (ForbiddenAccessException)
        {
            Context.Abort();
            return;
        }
        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        registry.Unregister(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    public Task<OnboardAudioStateResponse> GetState() => Run(() => service.GetStateAsync(CaptainId()));

    /// <summary>ACK: player đã nhận và chấp nhận xếp lệnh (chưa chắc đã phát). Idempotent theo RequestId.</summary>
    public Task<bool> AcceptPlayback(Guid requestId) =>
        Run(() => registry.IsActive(Context.ConnectionId) ? service.AcceptAsync(CaptainId(), requestId) : Task.FromResult(false));

    public Task<bool> ReportPlaybackState(Guid requestId, string state, string? message) => Run(async () =>
    {
        if (state is not ("Started" or "Completed" or "Failed")) throw new HubException("State phải là Started, Completed hoặc Failed.");
        if (!registry.IsActive(Context.ConnectionId)) return false; // kết nối chờ không điều khiển trạng thái lệnh
        return (await service.ReportStateAsync(CaptainId(), requestId, new ReportPlaybackStateRequest(state, message))).Applied;
    });

    private Guid CaptainId() => Guid.TryParse(Context.UserIdentifier, out var id) ? id : throw new HubException("Không xác định được tài khoản.");

    private static async Task<T> Run<T>(Func<Task<T>> action)
    {
        try { return await action(); }
        catch (Exception ex) when (ex is ForbiddenAccessException or NotFoundException) { throw new HubException(ex.Message); }
    }
}
