using WaterbusSystem.Application.Features.SightseeingAudio.Models;
using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Application.Interfaces.Repositories;

public sealed record PlaybackRequestView(Guid Id, Guid TripId, Guid? BoatId, string? AudioAssetUri,
    int? DurationSeconds, AudioPlaybackStatus Status);

/// <summary>Sổ cái lệnh phát audio. Mọi chuyển trạng thái là cập nhật có điều kiện nguyên tử trong SQL.</summary>
public interface IAudioPlaybackRepository
{
    /// <summary>Đã có lệnh được player nhận/đang phát/xong, hoặc đã hết lượt thử cho lượt ghé này.</summary>
    Task<bool> IsTriggeredAsync(Guid tripId, Guid routePoiKey, string visitKey, CancellationToken ct);
    Task<bool> TryReserveAsync(Guid requestId, Guid tripId, Guid routePoiKey, string visitKey,
        DateTimeOffset now, TimeSpan lease, int maxAttempts, CancellationToken ct);
    Task<bool> MarkAcceptedAsync(Guid requestId, DateTimeOffset now, CancellationToken ct);
    Task ReleaseAsync(Guid requestId, string? reason, int maxAttempts, CancellationToken ct);
    Task RecordDispatchAsync(AudioTriggerRequest request, CancellationToken ct);
    Task<PlaybackRequestView?> FindAsync(Guid requestId, CancellationToken ct);
    /// <summary>Player báo Started/Completed/Failed. Trả true nếu chuyển trạng thái được áp dụng (false = lặp/không hợp lệ).</summary>
    Task<bool> ReportAsync(Guid requestId, AudioPlaybackStatus target, string? reason, int maxAttempts,
        DateTimeOffset now, CancellationToken ct);
    Task<Guid?> FindBoatIdByCaptainAsync(Guid captainUserId, CancellationToken ct);
}
