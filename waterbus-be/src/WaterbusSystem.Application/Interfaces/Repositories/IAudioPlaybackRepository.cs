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
    /// <summary>Giữ chỗ lượt ghé. Trả epoch mới (≥ 1) nếu giành được (insert hoặc giành lại lease/Released), null nếu không.</summary>
    Task<int?> TryReserveAsync(Guid requestId, Guid tripId, Guid routePoiKey, string visitKey,
        DateTimeOffset now, TimeSpan lease, int maxAttempts, CancellationToken ct);
    /// <summary>Reserved → Accepted. <paramref name="epoch"/> != null: chỉ khi evaluation còn sở hữu reservation (null = ACK của player cho lệnh đã gửi).</summary>
    Task<bool> MarkAcceptedAsync(Guid requestId, int? epoch, DateTimeOffset now, CancellationToken ct);
    /// <summary>Nhả reservation; chỉ chủ hiện tại (đúng epoch) mới nhả được.</summary>
    Task ReleaseAsync(Guid requestId, int epoch, string? reason, int maxAttempts, CancellationToken ct);
    /// <summary>Ghi lượt gửi thật; chỉ thành công khi lệnh còn Reserved VÀ đúng epoch của người gọi.</summary>
    /// Đồng thời gia hạn lease (ReservedAt = now) để cửa sổ từ lúc ghi nhận tới lúc gửi/ACK nằm trọn trong một lease.
    Task<bool> RecordDispatchAsync(AudioTriggerRequest request, int epoch, DateTimeOffset now, CancellationToken ct);
    Task<PlaybackRequestView?> FindAsync(Guid requestId, CancellationToken ct);
    /// <summary>Player báo Started/Completed/Failed. Trả true nếu chuyển trạng thái được áp dụng (false = lặp/không hợp lệ).</summary>
    Task<bool> ReportAsync(Guid requestId, AudioPlaybackStatus target, string? reason, int maxAttempts,
        DateTimeOffset now, CancellationToken ct);
    Task<Guid?> FindBoatIdByCaptainAsync(Guid captainUserId, CancellationToken ct);
}
