using Microsoft.Extensions.Options;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.OnboardAudio;
using WaterbusSystem.Application.DTOs.Responses.OnboardAudio;
using WaterbusSystem.Application.Features.SightseeingAudio;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;

namespace WaterbusSystem.Application.Services.SightseeingAudio;

/// <summary>Nghiệp vụ phía Onboard Player: định danh Boat, trạng thái được phép phát, ACK, báo trạng thái, đọc MP3 có kiểm soát.</summary>
public sealed class OnboardAudioService(
    IAudioPlaybackRepository playback,
    ISightseeingAudioRepository audio,
    ITripRepository trips,
    IAudioStorageService storage,
    IOnboardPlayerRegistry registry,
    IOptions<SightseeingAudioOptions> options) : IOnboardAudioService
{
    private static readonly TripStatus[] ActiveStatuses = [TripStatus.Boarding, TripStatus.EnRoute, TripStatus.Suspended];

    public async Task<Guid> ResolveBoatAsync(Guid captainId, CancellationToken ct = default) =>
        await playback.FindBoatIdByCaptainAsync(captainId, ct)
        ?? throw new ForbiddenAccessException("Tài khoản không phải thuyền trưởng của tàu nào.");

    public async Task<OnboardAudioStateResponse> GetStateAsync(Guid captainId, CancellationToken ct = default)
    {
        var boatId = await ResolveBoatAsync(captainId, ct);
        var tripId = await trips.FindLatestTripIdByBoatAsync(boatId, ActiveStatuses, ct);
        if (tripId is null) return new(boatId, null, null, null, false, "NoActiveTrip");
        var snapshot = await audio.GetTrackingSnapshotAsync(tripId.Value, boatId, ct);
        if (snapshot is null) return new(boatId, tripId, null, null, false, "NoActiveTrip");
        var t = snapshot.Trip;
        var reason = t.TripType != TripType.Sightseeing ? "NotSightseeing"
            : t.Status != TripStatus.EnRoute ? "TripNot" + t.Status
            : t.IsSafetyAnnouncementActive ? "SafetyAnnouncementActive" : "Allowed";
        return new(boatId, t.TripId, t.Status.ToString(), t.TripType.ToString(), reason == "Allowed", reason);
    }

    public async Task<bool> AcceptAsync(Guid captainId, Guid requestId, CancellationToken ct = default)
    {
        var view = await RequireOwnRequestAsync(captainId, requestId, ct);
        // Ghi Accepted vào SQL TRƯỚC khi đánh thức dispatcher: player có thể báo Started ngay sau ACK mà không bị từ chối
        // vì sổ cái còn Reserved. MarkAccepted có điều kiện (chỉ từ Reserved) nên lặp ACK không mở lại/lùi trạng thái.
        if (view.Status == AudioPlaybackStatus.Reserved)
        {
            await playback.MarkAcceptedAsync(requestId, DateTimeOffset.UtcNow, ct);
            view = await playback.FindAsync(requestId, ct) ?? view;
        }
        if (view.Status is not (AudioPlaybackStatus.Accepted or AudioPlaybackStatus.Started or AudioPlaybackStatus.Completed))
            return false; // Released/Failed: lệnh đã hủy, ACK muộn không hồi sinh
        registry.SignalAck(requestId); // idempotent; không có dispatch đang chờ thì bỏ qua
        return true;
    }

    public async Task<PlaybackStateAck> ReportStateAsync(Guid captainId, Guid requestId, ReportPlaybackStateRequest request, CancellationToken ct = default)
    {
        await RequireOwnRequestAsync(captainId, requestId, ct);
        var target = request.State switch
        {
            "Started" => AudioPlaybackStatus.Started,
            "Completed" => AudioPlaybackStatus.Completed,
            _ => AudioPlaybackStatus.Failed
        };
        var applied = await playback.ReportAsync(requestId, target, request.Message, options.Value.MaxDispatchAttempts,
            DateTimeOffset.UtcNow, ct);
        return new PlaybackStateAck(requestId, applied);
    }

    public async Task<StoredAudio> GetAudioAsync(Guid captainId, Guid requestId, CancellationToken ct = default)
    {
        var view = await RequireOwnRequestAsync(captainId, requestId, ct);
        if (view.Status is AudioPlaybackStatus.Released or AudioPlaybackStatus.Failed)
            throw new ConcurrencyException("Lệnh phát đã bị hủy hoặc thất bại.");
        // BR-EXP-06: không cấp nội dung mới khi chuyến không còn được phép phát.
        var state = await GetStateAsync(captainId, ct);
        if (!state.PlaybackAllowed || state.TripId != view.TripId)
            throw new ConcurrencyException("Chuyến hiện không được phép phát audio: " + state.Reason);
        if (string.IsNullOrWhiteSpace(view.AudioAssetUri)) throw new NotFoundException("AudioAsset", requestId);
        try
        {
            return await storage.ReadAudioAsync(AssetKeyOf(view.AudioAssetUri), ct)
                ?? throw new NotFoundException("AudioAsset", requestId);
        }
        catch (AudioStorageException)
        {
            throw new ConcurrencyException("Kho lưu trữ audio tạm thời không khả dụng.");
        }
    }

    /// <summary>Lệnh không thuộc Boat của Captain bị coi như không tồn tại (không lộ sự tồn tại).</summary>
    private async Task<PlaybackRequestView> RequireOwnRequestAsync(Guid captainId, Guid requestId, CancellationToken ct)
    {
        var boatId = await ResolveBoatAsync(captainId, ct);
        var view = await playback.FindAsync(requestId, ct);
        if (view is null || view.BoatId != boatId) throw new NotFoundException("AudioPlaybackRequest", requestId);
        return view;
    }

    /// <summary>AudioAssetUri có thể là URI blob tuyệt đối; storage đọc theo khóa tương đối kết thúc bằng .mp3.</summary>
    private static string AssetKeyOf(string assetUri)
    {
        var idx = assetUri.IndexOf("/audio-assets/", StringComparison.Ordinal);
        return idx >= 0 ? assetUri[(idx + 1)..] : assetUri;
    }
}
