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
    public const string ReservationLostReason = "ReservationLost";

    /// <summary>Chính sách một player hoạt động: kết nối mới nhất còn là Captain hiện hành của Boat; thu hồi kết nối của Captain cũ.</summary>
    private async Task<string?> ResolveActiveAsync(Guid boatId, string group, CancellationToken ct)
    {
        string? active = null;
        foreach (var (connectionId, captainId) in registry.Connections(boatId))
        {
            if (await playback.FindBoatIdByCaptainAsync(captainId, ct) == boatId) { active = connectionId; continue; }
            registry.Unregister(connectionId);
            await hub.Groups.RemoveFromGroupAsync(connectionId, group, ct);
        }
        return active;
    }

    public async Task<AudioTriggerDispatchResult> DispatchTriggerRequestAsync(AudioTriggerRequest request, CancellationToken cancellationToken = default)
    {
        // Captain có thể đã bị đổi/ngừng sau khi kết nối: thu hồi kết nối của người không còn là Captain của Boat này
        // (rời group để không nhận lệnh/nội dung) trước khi gửi.
        var group = RealtimeContract.Groups.OnboardAudio(request.BoatId);
        if (await ResolveActiveAsync(request.BoatId, group, cancellationToken) is null)
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

        // Chọn lại player hoạt động SAU các bước chậm (kho lưu trữ): có thể đã có kết nối mới hơn thay thế.
        var activeConnection = await ResolveActiveAsync(request.BoatId, group, cancellationToken);
        if (activeConnection is null)
            return AudioTriggerDispatchResult.Failed(request.RequestId, "OnboardPlayerOffline");
        // Fence theo epoch: chỉ chủ hiện tại của reservation mới được ghi lượt gửi và gửi lệnh.
        if (request.ReservationEpoch is not { } epoch ||
            !await playback.RecordDispatchAsync(request, epoch, DateTimeOffset.UtcNow, cancellationToken))
            return AudioTriggerDispatchResult.Failed(request.RequestId, ReservationLostReason);
        using var ack = registry.PrepareAck(request.RequestId);
        var message = new AudioTriggerMessage(request.RequestId, request.TripId, request.BoatId, request.RoutePoiId, request.PoiId,
            request.PoiName, request.AudioGuideId, $"/api/v1/onboard-audio/requests/{request.RequestId}/audio", "audio/mpeg",
            request.DurationSeconds, request.DistanceMeters, request.TriggeredAt);
        await hub.Clients.Client(activeConnection)
            .SendAsync(RealtimeContract.Events.AudioTrigger, message, cancellationToken);

        var accepted = await ack.WaitAsync(TimeSpan.FromSeconds(Math.Max(1, options.Value.AckTimeoutSeconds)), cancellationToken);
        return accepted
            ? AudioTriggerDispatchResult.PlayerAccepted(request.RequestId)
            : AudioTriggerDispatchResult.SentWithoutPlayerAck(request.RequestId);
    }
}
