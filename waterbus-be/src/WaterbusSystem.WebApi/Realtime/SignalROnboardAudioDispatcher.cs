using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.SightseeingAudio;
using WaterbusSystem.Application.Features.SightseeingAudio.Interfaces;
using WaterbusSystem.Application.Features.SightseeingAudio.Models;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.WebApi.Hubs;

namespace WaterbusSystem.WebApi.Realtime;

/// <summary>
/// Gửi lệnh phát tới Onboard Player của đúng Boat qua SignalR và chờ ACK "đã nhận lệnh" (có timeout).
/// Trước khi gửi: player phải đang kết nối và asset phải tồn tại trong kho; nếu không trả Failed (lượt ghé được nhả,
/// lần GPS sau thử lại mà không tiêu hao lượt thử). Không bao giờ báo "đã phát ra loa" (BR-EXP-03).
/// </summary>
public sealed class SignalROnboardAudioDispatcher(
    IHubContext<OnboardAudioHub> hub,
    IOnboardPlayerRegistry registry,
    IAudioStorageService storage,
    IAudioPlaybackRepository playback,
    IOptions<SightseeingAudioOptions> options,
    ILogger<SignalROnboardAudioDispatcher> logger) : IOnboardAudioTriggerDispatcher
{
    public async Task<AudioTriggerDispatchResult> DispatchTriggerRequestAsync(AudioTriggerRequest request, CancellationToken cancellationToken = default)
    {
        // Captain có thể đã bị đổi/ngừng sau khi kết nối: thu hồi kết nối của người không còn là Captain của Boat này
        // (rời group để không nhận lệnh/nội dung) trước khi gửi.
        var group = RealtimeContract.Groups.OnboardAudio(request.BoatId);
        var live = 0;
        foreach (var (connectionId, captainId) in registry.Connections(request.BoatId))
        {
            if (await playback.FindBoatIdByCaptainAsync(captainId, cancellationToken) == request.BoatId) { live++; continue; }
            registry.Unregister(connectionId);
            await hub.Groups.RemoveFromGroupAsync(connectionId, group, cancellationToken);
        }
        if (live == 0)
            return AudioTriggerDispatchResult.Failed(request.RequestId, "OnboardPlayerOffline");

        try
        {
            if (!await storage.ExistsAsync(request.AudioAssetUri, cancellationToken))
            {
                logger.LogWarning("Audio asset của POI {PoiId} không tồn tại trong kho.", request.PoiId);
                return AudioTriggerDispatchResult.Failed(request.RequestId, "AudioAssetMissing");
            }
        }
        catch (AudioStorageException)
        {
            return AudioTriggerDispatchResult.Failed(request.RequestId, "AudioStorageUnavailable");
        }

        await playback.RecordDispatchAsync(request, cancellationToken);
        using var ack = registry.PrepareAck(request.RequestId);
        var message = new AudioTriggerMessage(request.RequestId, request.TripId, request.BoatId, request.RoutePoiId, request.PoiId,
            request.PoiName, request.AudioGuideId, $"/api/v1/onboard-audio/requests/{request.RequestId}/audio", "audio/mpeg",
            request.DurationSeconds, request.DistanceMeters, request.TriggeredAt);
        await hub.Clients.Group(group)
            .SendAsync(RealtimeContract.Events.AudioTrigger, message, cancellationToken);

        var accepted = await ack.WaitAsync(TimeSpan.FromSeconds(Math.Max(1, options.Value.AckTimeoutSeconds)), cancellationToken);
        return accepted
            ? AudioTriggerDispatchResult.PlayerAccepted(request.RequestId)
            : AudioTriggerDispatchResult.SentWithoutPlayerAck(request.RequestId);
    }
}
