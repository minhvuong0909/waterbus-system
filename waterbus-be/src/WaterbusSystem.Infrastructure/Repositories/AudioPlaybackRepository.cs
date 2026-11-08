using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Features.SightseeingAudio.Models;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Infrastructure.Persistence;

namespace WaterbusSystem.Infrastructure.Repositories;

/// <summary>
/// Sổ cái lệnh phát audio trên SQL Server. Chống phát trùng bằng UNIQUE (TripId, RoutePoiKey, VisitKey);
/// mọi chuyển trạng thái là UPDATE có điều kiện (ExecuteUpdate) nên đúng với nhiều GPS evaluation / instance đồng thời.
/// </summary>
public sealed class AudioPlaybackRepository(ApplicationDbContext context) : IAudioPlaybackRepository
{
    public Task<bool> IsTriggeredAsync(Guid tripId, Guid routePoiKey, string visitKey, CancellationToken ct) =>
        context.AudioPlaybackRequests.AsNoTracking().AnyAsync(x => x.TripId == tripId && x.RoutePoiKey == routePoiKey &&
            x.VisitKey == visitKey && (x.Status == AudioPlaybackStatus.Accepted || x.Status == AudioPlaybackStatus.Started ||
            x.Status == AudioPlaybackStatus.Completed || x.Status == AudioPlaybackStatus.Failed), ct);

    public async Task<bool> TryReserveAsync(Guid requestId, Guid tripId, Guid routePoiKey, string visitKey,
        DateTimeOffset now, TimeSpan lease, int maxAttempts, CancellationToken ct)
    {
        var entity = new AudioPlaybackRequest
        { Id = requestId, TripId = tripId, RoutePoiKey = routePoiKey, VisitKey = visitKey, ReservedAt = now };
        context.AudioPlaybackRequests.Add(entity);
        try
        {
            await context.SaveChangesAsync(ct);
            context.Entry(entity).State = EntityState.Detached;
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            context.Entry(entity).State = EntityState.Detached;
        }

        // Lượt ghé đã có dòng: chỉ giành lại khi lệnh trước được nhả (còn lượt thử) hoặc lease reservation đã treo.
        var leaseCutoff = now - lease;
        var rows = await context.AudioPlaybackRequests
            .Where(x => x.Id == requestId && x.Attempts < maxAttempts &&
                (x.Status == AudioPlaybackStatus.Released ||
                 (x.Status == AudioPlaybackStatus.Reserved && x.ReservedAt < leaseCutoff)))
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, AudioPlaybackStatus.Reserved)
                .SetProperty(x => x.ReservedAt, now)
                .SetProperty(x => x.FailureReason, (string?)null), ct);
        return rows == 1;
    }

    public async Task<bool> MarkAcceptedAsync(Guid requestId, DateTimeOffset now, CancellationToken ct) =>
        await context.AudioPlaybackRequests.Where(x => x.Id == requestId && x.Status == AudioPlaybackStatus.Reserved)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, AudioPlaybackStatus.Accepted)
                .SetProperty(x => x.AcceptedAt, now), ct) == 1;

    public Task ReleaseAsync(Guid requestId, string? reason, int maxAttempts, CancellationToken ct) =>
        context.AudioPlaybackRequests.Where(x => x.Id == requestId && x.Status == AudioPlaybackStatus.Reserved)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, x => x.Attempts >= maxAttempts ? AudioPlaybackStatus.Failed : AudioPlaybackStatus.Released)
                .SetProperty(x => x.FailureReason, Trim(reason)), ct);

    public Task RecordDispatchAsync(AudioTriggerRequest r, CancellationToken ct) =>
        context.AudioPlaybackRequests.Where(x => x.Id == r.RequestId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.BoatId, r.BoatId).SetProperty(x => x.PoiId, r.PoiId)
                .SetProperty(x => x.Attempts, x => x.Attempts + 1)
                .SetProperty(x => x.AudioGuideId, r.AudioGuideId).SetProperty(x => x.AudioAssetUri, r.AudioAssetUri)
                .SetProperty(x => x.DurationSeconds, r.DurationSeconds).SetProperty(x => x.DistanceMeters, r.DistanceMeters), ct);

    public Task<PlaybackRequestView?> FindAsync(Guid requestId, CancellationToken ct) =>
        context.AudioPlaybackRequests.AsNoTracking().Where(x => x.Id == requestId)
            .Select(x => new PlaybackRequestView(x.Id, x.TripId, x.BoatId, x.AudioAssetUri, x.DurationSeconds, x.Status))
            .FirstOrDefaultAsync(ct);

    public async Task<bool> ReportAsync(Guid requestId, AudioPlaybackStatus target, string? reason, int maxAttempts,
        DateTimeOffset now, CancellationToken ct)
    {
        var rows = context.AudioPlaybackRequests.Where(x => x.Id == requestId);
        return target switch
        {
            AudioPlaybackStatus.Started => await rows.Where(x => x.Status == AudioPlaybackStatus.Accepted)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, AudioPlaybackStatus.Started)
                    .SetProperty(x => x.StartedAt, now), ct) == 1,
            AudioPlaybackStatus.Completed => await rows
                .Where(x => x.Status == AudioPlaybackStatus.Accepted || x.Status == AudioPlaybackStatus.Started)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, AudioPlaybackStatus.Completed)
                    .SetProperty(x => x.CompletedAt, now), ct) == 1,
            // Player báo lỗi: còn lượt thử thì nhả để lần GPS sau gửi lại cùng RequestId, hết lượt thì Failed.
            _ => await rows.Where(x => x.Status == AudioPlaybackStatus.Accepted || x.Status == AudioPlaybackStatus.Started ||
                    x.Status == AudioPlaybackStatus.Reserved)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.Status, x => x.Attempts >= maxAttempts ? AudioPlaybackStatus.Failed : AudioPlaybackStatus.Released)
                    .SetProperty(x => x.FailureReason, Trim(reason)), ct) == 1
        };
    }

    public async Task<Guid?> FindBoatIdByCaptainAsync(Guid captainUserId, CancellationToken ct) =>
        await context.Boats.AsNoTracking().Where(b => b.CaptainUserId == captainUserId && b.IsActive && !b.IsDeleted)
            .Select(b => (Guid?)b.Id).FirstOrDefaultAsync(ct);

    private static string? Trim(string? reason) =>
        string.IsNullOrWhiteSpace(reason) ? null : reason.Length > 300 ? reason[..300] : reason;
}
